using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Crea, edita (renombra) y borra las entradas del diario: una nota por entrada más sus imágenes, en <c>Diario/</c> del PJ.
/// Toda escritura lleva precondición (<c>MustNotExist</c> o <c>UnchangedSince</c>). Solo borra ficheros de la entrada, en
/// operaciones explícitas del DM, o lo que la misma operación acaba de crear si hay que deshacerla.
/// </summary>
public sealed partial class JournalStore(VaultHost host, TimeProvider time, ILogger<JournalStore> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private enum ImageOutcome { Written, Unreadable, Exists }

    // Lo que una operación ha hecho y habría que revertir.
    private sealed class Undo
    {
        public List<(string From, string To)> Moves { get; } = [];
        public List<string> Created { get; } = [];
    }

    // Nota leída para editarla o borrarla. Entry es null si el YAML es inválido o no es type: journal.
    private sealed record Loaded(VaultFileContent File, IReadOnlyDictionary<string, object?> Fields, JournalEntry? Entry, bool YamlBroken);

    public Task<JournalResult> CreateAsync(JournalCreate request, CancellationToken ct = default) =>
        RunAsync(vault => CreateCoreAsync(vault, request, ct), ct);

    public Task<JournalResult> DeleteAsync(string entryPath, DateTime expectedLastWriteUtc, CancellationToken ct = default) =>
        RunAsync(vault => DeleteCoreAsync(vault, VaultPaths.Normalize(entryPath), expectedLastWriteUtc, ct), ct);

    private async Task<JournalResult> RunAsync(Func<VaultService, Task<JournalResult>> operation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return host.Current is { } vault ? await operation(vault) : new JournalResult(JournalStatus.NoVault);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<JournalResult> CreateCoreAsync(VaultService vault, JournalCreate request, CancellationToken ct)
    {
        var accepted = request.Images.Where(image => JournalPaths.HasSupportedExtension(image.FileName)).ToList();
        var unreadable = request.Images.Count - accepted.Count;
        if (accepted.Count == 0) return new JournalResult(JournalStatus.NoImages, null, unreadable);

        var folder = JournalPaths.DiaryFolderOf(request.PcNotePath);
        var added = time.GetLocalNow();
        var taken = await TakenNamesAsync(vault, folder, ct);
        var baseName = JournalNames.Unique(JournalNames.BaseName(added, request.Title), name => IsTaken(taken, name));
        var undo = new Undo();
        var names = new List<string>();
        try
        {
            var (exists, skipped) = await WriteCreatedImagesAsync(vault, folder, baseName, accepted, names, undo, ct);
            unreadable += skipped;
            if (exists) return await FailAsync(vault, undo, JournalStatus.Failed, unreadable);
            if (names.Count == 0) return new JournalResult(JournalStatus.NoImages, null, unreadable);

            var notePath = $"{folder}/{baseName}.md";
            var text = JournalNote.Compose(added, names, request.Creature, request.Note);
            var saved = await vault.SaveAsync(notePath, text, WritePrecondition.MustNotExist, ct);
            return saved.Status == WriteStatus.Written
                ? new JournalResult(JournalStatus.Done, notePath, unreadable)
                : await FailAsync(vault, undo, JournalStatus.Failed, unreadable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("No se pudo crear la entrada del diario ({ExceptionType}).", ex.GetType().FullName);
            return await FailAsync(vault, undo, JournalStatus.Failed, unreadable);
        }
        catch (OperationCanceledException)
        {
            await UndoAsync(vault, undo);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError("Error inesperado al {Operation} una entrada del diario ({ExceptionType}).", "crear", ex.GetType().FullName);
            await UndoAsync(vault, undo);
            throw;
        }
    }

    // Exists: una imagen ya estaba en el vault (alguien se adelantó); hay que deshacer.
    private async Task<(bool Exists, int Unreadable)> WriteCreatedImagesAsync(VaultService vault, string folder, string baseName,
        IReadOnlyList<JournalImageSource> sources, List<string> names, Undo undo, CancellationToken ct)
    {
        var unreadable = 0;
        foreach (var source in sources)
        {
            var name = JournalNames.ImageName(baseName, names.Count + 1, JournalPaths.ExtensionOf(source.FileName));
            var outcome = await WriteImageAsync(vault, $"{folder}/{name}", source, ct);
            if (outcome == ImageOutcome.Exists) return (true, unreadable);
            if (outcome == ImageOutcome.Unreadable)
            {
                unreadable++;
                continue;
            }
            undo.Created.Add($"{folder}/{name}");
            names.Add(name);
        }
        return (false, unreadable);
    }

    private async Task<JournalResult> DeleteCoreAsync(VaultService vault, string path, DateTime expectedLastWriteUtc, CancellationToken ct)
    {
        if (!JournalPaths.IsEntryPath(path)) return new JournalResult(JournalStatus.Failed);
        if (await LoadAsync(vault, path, ct) is not { } loaded) return new JournalResult(JournalStatus.NotFound);
        if (loaded.File.LastWriteTimeUtc != expectedLastWriteUtc) return new JournalResult(JournalStatus.ModifiedOnDisk);
        if (loaded.Entry is not { } entry) return new JournalResult(JournalStatus.Failed);
        if (await ImagesCitedByOthersAsync(vault, entry, path, ct) is not { } shared) return new JournalResult(JournalStatus.Failed);
        try
        {
            await vault.DeleteAsync(path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("No se pudo borrar la nota de una entrada del diario ({ExceptionType}).", ex.GetType().FullName);
            return new JournalResult(JournalStatus.Failed);
        }
        // Solo se borra lo que es una imagen: una errata en el YAML (una .md hermana) no puede llevarse otra nota.
        // Y no la que cita otra entrada (p. ej. si el DM duplicó la nota en Obsidian).
        foreach (var image in entry.Images.Where(image => JournalPaths.HasSupportedExtension(image.Name) && !shared.Contains(image.Name)))
            await TryDeleteAsync(vault, image.Path);
        return new JournalResult(JournalStatus.Done, path);
    }

    // Nombres de imagen que citan las demás notas de la carpeta, leídas del disco (no del índice en memoria).
    // Si alguna no se puede interpretar (YAML roto), no se sabe qué cita: se protegen todas las imágenes de la entrada.
    // Null si no se pudo leer una nota (p. ej. un bloqueo de Syncthing): quien llama no debe tocar nada.
    private async Task<HashSet<string>?> ImagesCitedByOthersAsync(VaultService vault, JournalEntry entry, string notePath, CancellationToken ct)
    {
        var cited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var files = await vault.ListFilesAsync(path => JournalPaths.FolderOf(path).Equals(entry.DiaryFolder, StringComparison.OrdinalIgnoreCase) && VaultPaths.IsMarkdown(path), ct);
            foreach (var file in files)
            {
                var other = VaultPaths.Normalize(file.RelativePath);
                if (other.Equals(notePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (await LoadAsync(vault, other, ct) is not { } loaded) continue;
                if (loaded.YamlBroken)
                    foreach (var image in entry.Images) cited.Add(image.Name);
                else if (loaded.Entry is { } sibling)
                    foreach (var image in sibling.Images) cited.Add(image.Name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("No se pudo leer otra nota del diario ({ExceptionType}).", ex.GetType().FullName);
            return null;
        }
        return cited;
    }

    private async Task<Loaded?> LoadAsync(VaultService vault, string path, CancellationToken ct)
    {
        VaultFileContent file;
        try
        {
            file = await vault.ReadRawAsync(path, ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        var parsed = FrontmatterParser.Parse(file.Content);
        // Decisión 13: con YAML inválido o sin type: journal la nota no se reescribe ni se borra.
        var entry = parsed.HasError || !JournalReader.IsJournal(parsed.Fields)
            ? null
            : JournalReader.Read(path, parsed.Fields, parsed.Body, null, file.LastWriteTimeUtc, _ => true);
        return new Loaded(file, parsed.Fields, entry, parsed.HasError);
    }

    // Se lee todo el flujo antes de escribir: un fallo de lectura (imagen ilegible) se distingue de uno de escritura (vault).
    private async Task<ImageOutcome> WriteImageAsync(VaultService vault, string path, JournalImageSource source, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        try
        {
            await using var stream = await source.OpenAsync();
            await stream.CopyToAsync(buffer, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Cualquier fallo al abrir o leer la fuente (p. ej. un IBrowserFile caducado) es una imagen ilegible, no un fallo de la entrada.
            logger.LogWarning("No se pudo leer una imagen del diario ({ExceptionType}).", ex.GetType().FullName);
            return ImageOutcome.Unreadable;
        }
        if (buffer.Length == 0) return ImageOutcome.Unreadable;
        buffer.Position = 0;
        var result = await vault.WriteBytesAsync(path, buffer, WritePrecondition.MustNotExist, ct);
        return result.Status == WriteStatus.Written ? ImageOutcome.Written : ImageOutcome.Exists;
    }

    // Nombres de todos los ficheros de la carpeta del diario (el vault entero se recorre una vez por operación).
    private static async Task<HashSet<string>> TakenNamesAsync(VaultService vault, string folder, CancellationToken ct)
    {
        var files = await vault.ListFilesAsync(path => JournalPaths.FolderOf(path).Equals(folder, StringComparison.OrdinalIgnoreCase), ct);
        return new HashSet<string>(files.Select(file => VaultPaths.Normalize(file.RelativePath)[(folder.Length + 1)..]), StringComparer.OrdinalIgnoreCase);
    }

    // Un nombre base está ocupado si ya hay una nota con él o imágenes que empiezan por «<base>-».
    private static bool IsTaken(HashSet<string> names, string baseName) =>
        names.Contains(baseName + ".md") || names.Any(name => name.StartsWith(baseName + "-", StringComparison.OrdinalIgnoreCase));

    // Deshacer: devuelve los movimientos y borra lo que esta misma operación creó. Nunca lanza.
    private async Task UndoAsync(VaultService vault, Undo undo)
    {
        for (var i = undo.Moves.Count - 1; i >= 0; i--)
        {
            var (original, moved) = undo.Moves[i];
            try
            {
                if (!await vault.MoveAsync(moved, original))
                    logger.LogWarning("No se pudo devolver una imagen del diario a su sitio: {Path}.", moved);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError("No se pudo devolver una imagen del diario a su sitio ({ExceptionType}).", ex.GetType().FullName);
            }
        }
        foreach (var path in undo.Created) await TryDeleteAsync(vault, path);
    }

    private async Task TryDeleteAsync(VaultService vault, string path)
    {
        try
        {
            await vault.DeleteAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo borrar un fichero del diario ({ExceptionType}).", ex.GetType().FullName);
        }
    }
}
