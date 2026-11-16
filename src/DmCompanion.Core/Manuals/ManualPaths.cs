using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Manuals;

public static class ManualPaths
{
    public const string Root = "manuales";
    private const string PdfExtension = ".pdf";
    private const string BookmarksSuffix = ".marcadores.json";

    /// <summary>
    /// true si es un .pdf bajo manuales/&lt;systemId&gt;/ (subcarpetas incluidas), sin distinguir mayúsculas.
    /// Las copias <c>*.sync-conflict-*</c> de Syncthing no son manuales.
    /// </summary>
    public static bool IsManualOf(string relativePath, string systemId)
    {
        var path = VaultPaths.Normalize(relativePath);
        var prefix = $"{Root}/{systemId}/";
        return path.Length > prefix.Length
            && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase)
            && !VaultPaths.IsConflictFile(path);
    }

    /// <summary>Subcarpeta dentro de manuales/&lt;id&gt;/ ("" si está en la raíz del sistema).</summary>
    public static string GroupOf(string relativePath, string systemId)
    {
        var path = VaultPaths.Normalize(relativePath);
        var prefixLength = $"{Root}/{systemId}/".Length;
        if (path.Length <= prefixLength) return string.Empty;
        var inner = path[prefixLength..];
        var slash = inner.LastIndexOf('/');
        return slash < 0 ? string.Empty : inner[..slash];
    }

    public static string TitleOf(string relativePath)
    {
        var path = VaultPaths.Normalize(relativePath);
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase) ? name[..^PdfExtension.Length] : name;
    }

    public static string BookmarksPathOf(string pdfPath)
    {
        var path = VaultPaths.Normalize(pdfPath);
        return (path.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase) ? path[..^PdfExtension.Length] : path)
            + BookmarksSuffix;
    }

    /// <summary>true si es un fichero de marcadores (<c>*.marcadores.json</c>) bajo manuales/, sin distinguir mayúsculas.</summary>
    public static bool IsBookmarksFile(string relativePath)
    {
        var path = VaultPaths.Normalize(relativePath);
        return path.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(BookmarksSuffix, StringComparison.OrdinalIgnoreCase);
    }

    public static string PositionKeyOf(string pdfPath) => "manual.page." + VaultPaths.Normalize(pdfPath);
}
