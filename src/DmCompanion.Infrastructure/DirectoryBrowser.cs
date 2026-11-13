using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class DirectoryBrowser : IFolderBrowser
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    // Límites de la búsqueda: no se recorre un árbol enorme (p. ej. toda una unidad o el perfil del usuario).
    private const int MaxDepth = 2;
    private const int MaxEntries = 2000;

    public bool LooksLikeVault(string path)
    {
        // Una carpeta que no existe no necesita aviso: abrirla ya falla con su propio error.
        if (!Directory.Exists(path)) return true;
        var full = Path.GetFullPath(path);
        // Sin barra final en ambos lados: una raíz UNC con barra final no coincide con lo que devuelve GetPathRoot.
        if (Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!) == Path.TrimEndingDirectorySeparator(full)) return false;
        if (Directory.Exists(Path.Combine(full, ".obsidian"))) return true;

        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = MaxDepth, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        try
        {
            return Directory.EnumerateFileSystemEntries(full, "*", options)
                .Take(MaxEntries)
                .Any(entry => entry.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
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
