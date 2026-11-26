using System.Text;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class FileSystemVaultCreator : IVaultCreator
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public async Task<CreateVaultResult> CreateAsync(string folderPath, string campaignName, string systemId, CancellationToken ct = default)
    {
        if (!Directory.Exists(folderPath))
            return new(CreateVaultStatus.FolderMissing, "La carpeta ya no existe.");
        var full = Path.GetFullPath(folderPath);
        if (Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!) == Path.TrimEndingDirectorySeparator(full))
            return new(CreateVaultStatus.DriveRoot, "No se puede crear un vault en la raíz de una unidad.");

        var plan = VaultScaffold.Plan(campaignName, GameSystemRegistry.Default.Get(systemId));
        if (plan is null)
            return new(CreateVaultStatus.InvalidName, "Escribe un nombre de campaña con letras o números.");

        var notePath = Path.Combine(full, plan.NoteRelativePath);
        var obsidian = Path.Combine(full, ".obsidian");
        if (File.Exists(notePath) || Directory.Exists(notePath))
            return new(CreateVaultStatus.NoteExists, $"Ya existe «{plan.NoteRelativePath}» en esta carpeta.");
        if (File.Exists(obsidian) || Directory.Exists(obsidian))
            return new(CreateVaultStatus.ObsidianExists, "Ya existe una carpeta «.obsidian» en esta carpeta.");

        try
        {
            await WriteNewAsync(notePath, plan.NoteContent, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(CreateVaultStatus.Failed, $"No se pudo crear la nota de la campaña; puede haber quedado un fichero vacío con ese nombre ({ex.Message}).");
        }

        try
        {
            Directory.CreateDirectory(obsidian);
            await WriteNewAsync(Path.Combine(obsidian, "app.json"), plan.AppJson, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(CreateVaultStatus.PartiallyCreated,
                $"Se creó la nota de la campaña y la carpeta ya funciona como vault de la app, pero no se pudo crear «.obsidian» ({ex.Message}). Obsidian no la reconocerá hasta que exista.");
        }

        return new(CreateVaultStatus.Created);
    }

    private static async Task WriteNewAsync(string path, string content, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(Utf8NoBom.GetBytes(content), ct);
    }
}
