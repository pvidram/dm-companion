using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class DirectoryBrowser : IFolderBrowser
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    // Límites de la búsqueda: no se recorre un árbol enorme (p. ej. toda una unidad o el perfil del usuario).
    private const int MaxDepth = 2;
    private const int MaxEntries = 2000;

    public bool LooksLikeVault(string path) => Classify(path) == FolderVaultStatus.Vault;

    public FolderVaultStatus Classify(string path)
    {
        // Una carpeta que no existe no necesita aviso: abrirla ya falla con su propio error.
        if (!Directory.Exists(path)) return FolderVaultStatus.Vault;
        var full = Path.GetFullPath(path);
        // Sin barra final en ambos lados: una raíz UNC con barra final no coincide con lo que devuelve GetPathRoot.
        if (Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!) == Path.TrimEndingDirectorySeparator(full)) return FolderVaultStatus.DriveRoot;
        if (Directory.Exists(Path.Combine(full, ".obsidian"))) return FolderVaultStatus.Vault;

        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = MaxDepth, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        try
        {
            var taken = 0;
            foreach (var entry in Directory.EnumerateFileSystemEntries(full, "*", options).Take(MaxEntries))
            {
                taken++;
                if (entry.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return FolderVaultStatus.Vault;
            }

            return taken >= MaxEntries ? FolderVaultStatus.Unknown : FolderVaultStatus.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FolderVaultStatus.Unknown;
        }
    }

    public CreateFolderResult CreateDirectory(string parentPath, string name)
    {
        var check = FolderName.Check(name);
        if (!check.IsValid) return new(CreateFolderStatus.InvalidName, null, check.Error);
        if (!Directory.Exists(parentPath)) return new(CreateFolderStatus.ParentMissing, null, "La carpeta de destino ya no existe.");
        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
            var taken = Directory.EnumerateFileSystemEntries(parentPath, "*", options)
                .Any(entry => string.Equals(Path.GetFileName(entry), check.Name, StringComparison.OrdinalIgnoreCase));
            if (taken) return new(CreateFolderStatus.AlreadyExists, null, $"Ya existe «{check.Name}» en esta carpeta.");

            var created = Path.Combine(parentPath, check.Name!);
            Directory.CreateDirectory(created);
            return new(CreateFolderStatus.Created, created);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(CreateFolderStatus.Failed, null, $"No se pudo crear la carpeta ({ex.Message}).");
        }
    }

    public IReadOnlyList<string> ListSubdirectories(string path)
    {
        if (!Directory.Exists(path)) return [];
        return Directory.EnumerateDirectories(path, "*", new EnumerationOptions { IgnoreInaccessible = true })
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => !name.StartsWith('.'))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
