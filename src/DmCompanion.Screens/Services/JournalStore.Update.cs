using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

public sealed partial class JournalStore
{
    private sealed record Planned(string FinalName, string? OldName, JournalImageSource? Added);

    // Todo lo que hace falta para aplicar una edición ya validada.
    private sealed record EditPlan(string OldPath, Loaded Loaded, JournalEntry Current, string NewBase, bool Renamed,
        IReadOnlyList<string> Keep, List<Planned> Steps, int Unreadable, HashSet<string> Shared)
    {
        public string Folder => Current.DiaryFolder;
        public string NotePath => $"{Folder}/{NewBase}.md";
    }

    public Task<JournalResult> UpdateAsync(JournalEdit edit, CancellationToken ct = default) =>
        RunAsync(vault => UpdateCoreAsync(vault, edit, ct), ct);

    private async Task<JournalResult> UpdateCoreAsync(VaultService vault, JournalEdit edit, CancellationToken ct)
    {
        var oldPath = VaultPaths.Normalize(edit.EntryPath);
        if (!JournalPaths.IsEntryPath(oldPath)) return new JournalResult(JournalStatus.Failed);
        if (await LoadAsync(vault, oldPath, ct) is not { } loaded) return new JournalResult(JournalStatus.NotFound);
        if (loaded.File.LastWriteTimeUtc != edit.ExpectedLastWriteUtc) return new JournalResult(JournalStatus.ModifiedOnDisk);
        if (loaded.Entry is not { } current) return new JournalResult(JournalStatus.Failed);

        var keep = edit.Images.Where(item => item.ExistingName is not null).Select(item => item.ExistingName!).ToList();
        var known = current.Images.Select(image => image.Name).Where(JournalPaths.HasSupportedExtension).ToHashSet(StringComparer.Ordinal);
        if (keep.Any(name => !known.Contains(name)) || keep.Distinct(StringComparer.Ordinal).Count() != keep.Count)
            return new JournalResult(JournalStatus.Failed);
        var items = edit.Images
            .Where(item => item.ExistingName is not null || (item.Added is { } source && JournalPaths.HasSupportedExtension(source.FileName)))
            .ToList();
        var unreadable = edit.Images.Count - items.Count;
        if (items.Count == 0) return new JournalResult(JournalStatus.NoImages, null, unreadable);

        var taken = await TakenNamesAsync(vault, current.DiaryFolder, ct);
        var (newBase, renamed) = ChooseBase(current, oldPath, known, taken, edit.Title);
        var shared = await ImagesCitedByOthersAsync(vault, current.DiaryFolder, oldPath, ct);
        var plan = new EditPlan(oldPath, loaded, current, newBase, renamed, keep, PlanImages(items, newBase, renamed, taken, shared), unreadable, shared);
        return await ApplyAsync(vault, plan, edit, known, ct);
    }

    // Solo cambiar mayúsculas no renombra: en Windows y Android no se distinguen y la nota nueva «ya existiría».
    private static (string NewBase, bool Renamed) ChooseBase(JournalEntry current, string oldPath, HashSet<string> known,
        HashSet<string> taken, string? title)
    {
        var currentBase = VaultPaths.FileTitle(oldPath);
        var wantedBase = JournalNames.RenamedBase(currentBase, current.Added, title);
        if (wantedBase.Equals(currentBase, StringComparison.OrdinalIgnoreCase)) return (currentBase, false);
        // Los ficheros de esta misma entrada no cuentan como ocupados: solo cambia el nombre.
        taken.Remove(currentBase + ".md");
        foreach (var name in known) taken.Remove(name);
        var newBase = JournalNames.Unique(wantedBase, name => IsTaken(taken, name));
        return (newBase, !newBase.Equals(currentBase, StringComparison.Ordinal));
    }

    private async Task<JournalResult> ApplyAsync(VaultService vault, EditPlan plan, JournalEdit edit, HashSet<string> known, CancellationToken ct)
    {
        var undo = new Undo();
        var unreadable = plan.Unreadable;
        try
        {
            var written = await WriteNewImagesAsync(vault, plan, undo, ct);
            unreadable += written.Unreadable;
            if (written.Failed) return await FailAsync(vault, undo, JournalStatus.Failed, unreadable);
            if (plan.Steps.Count == 0) return await FailAsync(vault, undo, JournalStatus.NoImages, unreadable);
            if (!await MoveRenamedAsync(vault, plan, undo, ct)) return await FailAsync(vault, undo, JournalStatus.Failed, unreadable);

            var status = await WriteNoteAsync(vault, plan, edit, undo, ct);
            if (status != JournalStatus.Done) return await FailAsync(vault, undo, status, unreadable);

            // Lo último y lo único irreversible: las imágenes que el DM quitó.
            foreach (var name in known.Where(name => !plan.Keep.Contains(name, StringComparer.Ordinal) && !plan.Shared.Contains(name)))
                await TryDeleteAsync(vault, $"{plan.Folder}/{name}");
            return new JournalResult(JournalStatus.Done, plan.NotePath, unreadable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("No se pudo editar la entrada del diario ({ExceptionType}).", ex.GetType().FullName);
            return await FailAsync(vault, undo, JournalStatus.Failed, unreadable);
        }
        catch (OperationCanceledException)
        {
            await UndoAsync(vault, undo);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError("Error inesperado al {Operation} una entrada del diario ({ExceptionType}).", "editar", ex.GetType().FullName);
            await UndoAsync(vault, undo);
            throw;
        }
    }

    private async Task<JournalResult> FailAsync(VaultService vault, Undo undo, JournalStatus status, int unreadable)
    {
        await UndoAsync(vault, undo);
        return new JournalResult(status, null, unreadable);
    }

    // Escribe las imágenes nuevas; las ilegibles salen del plan y se cuentan.
    private async Task<(bool Failed, int Unreadable)> WriteNewImagesAsync(VaultService vault, EditPlan plan, Undo undo, CancellationToken ct)
    {
        var unreadable = 0;
        foreach (var step in plan.Steps.Where(p => p.Added is not null).ToList())
        {
            var path = $"{plan.Folder}/{step.FinalName}";
            var outcome = await WriteImageAsync(vault, path, step.Added!, ct);
            if (outcome == ImageOutcome.Exists) return (true, unreadable);
            if (outcome == ImageOutcome.Written)
            {
                undo.Created.Add(path);
                continue;
            }
            unreadable++;
            plan.Steps.Remove(step);
        }
        return (false, unreadable);
    }

    private static async Task<bool> MoveRenamedAsync(VaultService vault, EditPlan plan, Undo undo, CancellationToken ct)
    {
        if (!plan.Renamed) return true;
        foreach (var step in plan.Steps.Where(p => p.OldName is not null))
        {
            var (original, moved) = ($"{plan.Folder}/{step.OldName}", $"{plan.Folder}/{step.FinalName}");
            if (original == moved) continue; // la imagen que cita otra entrada conserva su nombre
            if (!await vault.MoveAsync(original, moved, ct)) return false;
            undo.Moves.Add((original, moved));
        }
        return true;
    }

    private async Task<JournalStatus> WriteNoteAsync(VaultService vault, EditPlan plan, JournalEdit edit, Undo undo, CancellationToken ct)
    {
        var text = JournalNote.Compose(plan.Current.Added, [.. plan.Steps.Select(p => p.FinalName)], edit.Creature, edit.Note, plan.Loaded.Fields);
        var stamp = plan.Loaded.File.LastWriteTimeUtc;
        var saved = plan.Renamed
            ? await vault.SaveAsync(plan.NotePath, text, WritePrecondition.MustNotExist, ct)
            : await vault.SaveAsync(plan.OldPath, text, WritePrecondition.UnchangedSince(stamp), ct);
        if (saved.Status != WriteStatus.Written)
            return saved.Status == WriteStatus.ModifiedOnDisk ? JournalStatus.ModifiedOnDisk : JournalStatus.Failed;
        if (!plan.Renamed) return JournalStatus.Done;

        undo.Created.Add(plan.NotePath);
        // La nota vieja se borra solo si sigue como se leyó: si alguien la tocó, se deshace todo.
        var stat = await vault.StatAsync(plan.OldPath, ct);
        if (stat is null || stat.LastWriteTimeUtc != stamp) return JournalStatus.ModifiedOnDisk;
        // Sin ct: si borrara y luego se cancelara, el deshacer borraría la nota nueva y se perdería la entrada.
        await vault.DeleteAsync(plan.OldPath);
        return JournalStatus.Done;
    }

    // Con renombrado, todas pasan a <base>-1…n en orden. Sin él, las que se conservan mantienen el nombre y las nuevas toman
    // el primer número libre (también se evitan los nombres de las imágenes que se van a quitar: se borran al final).
    // Una imagen que cita otra entrada conserva su nombre aunque se renombre: la nota nueva la sigue citando con el viejo.
    private static List<Planned> PlanImages(IReadOnlyList<JournalImageItem> items, string newBase, bool renamed, HashSet<string> taken,
        HashSet<string> shared)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var plan = new List<Planned>();
        foreach (var item in items)
        {
            var extension = JournalPaths.ExtensionOf(item.ExistingName ?? item.Added!.FileName);
            string finalName;
            if (item.ExistingName is not null && (!renamed || shared.Contains(item.ExistingName)))
            {
                finalName = item.ExistingName;
            }
            else
            {
                var number = renamed ? plan.Count + 1 : 1;
                while (!renamed && NumberTaken(used, newBase, number)) number++;
                finalName = JournalNames.ImageName(newBase, number, extension);
            }
            used.Add(finalName);
            plan.Add(new Planned(finalName, item.ExistingName, item.Added));
        }
        return plan;
    }

    // El número cuenta aunque la extensión sea otra: «-1.jpg» ocupa el 1 para «-1.png».
    private static bool NumberTaken(HashSet<string> used, string baseName, int number) =>
        used.Any(name => name.StartsWith($"{baseName}-{number}.", StringComparison.OrdinalIgnoreCase));
}
