using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Validación y tipo de contenido compartidos por los dos hosts (App y DevHost) para servir ficheros
/// del vault por URL virtual. Solo se sirven imágenes con ruta segura dentro del vault.
/// </summary>
public static class VaultFileRequest
{
    public const string Prefix = "/_vault/";

    /// <summary>
    /// Recibe la ruta y la query tal como llegan (codificadas). Quita la query, decodifica UNA sola vez
    /// y valida con <see cref="VaultPaths.TryNormalizeSafe"/> y <see cref="MapPaths.IsImage"/>.
    /// </summary>
    public static bool TryParse(string absolutePathAndQuery, out string relativePath)
    {
        relativePath = string.Empty;
        var end = absolutePathAndQuery.IndexOfAny(['?', '#']);
        var path = end < 0 ? absolutePathAndQuery : absolutePathAndQuery[..end];
        if (!path.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        string decoded;
        try { decoded = Uri.UnescapeDataString(path[Prefix.Length..]); }
        catch (UriFormatException) { return false; }

        if (!VaultPaths.TryNormalizeSafe(decoded, out var normalized) || !MapPaths.IsImage(normalized)) return false;
        relativePath = normalized;
        return true;
    }

    public static string ContentTypeOf(string relativePath) => Path.GetExtension(relativePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream",
    };
}
