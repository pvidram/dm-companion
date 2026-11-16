using DmCompanion.Core.Audio;
using DmCompanion.Core.Manuals;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Validación y tipo de contenido compartidos por los dos hosts (App y DevHost) para servir ficheros
/// del vault por URL virtual. Solo se sirven imágenes, audio y PDF (estos, solo bajo manuales/) con ruta segura dentro del vault.
/// </summary>
public static class VaultFileRequest
{
    public const string Prefix = "/_vault/";

    /// <summary>
    /// Recibe la ruta y la query tal como llegan (codificadas). Quita la query, decodifica UNA sola vez
    /// y valida con <see cref="VaultPaths.TryNormalizeSafe"/> y <see cref="MapPaths.IsImage"/> o <see cref="AudioPaths.IsAudio"/> o PDF.
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

        if (!VaultPaths.TryNormalizeSafe(decoded, out var normalized) || !(MapPaths.IsImage(normalized) || AudioPaths.IsAudio(normalized) || IsPdf(normalized))) return false;
        relativePath = normalized;
        return true;
    }

    private static bool IsPdf(string relativePath) =>
        relativePath.StartsWith(ManualPaths.Root + "/", StringComparison.OrdinalIgnoreCase)
        && relativePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public static string ContentTypeOf(string relativePath) => Path.GetExtension(relativePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".pdf" => "application/pdf",
        _ when AudioPaths.IsAudio(relativePath) => AudioPaths.ContentTypeOf(relativePath),
        _ => "application/octet-stream",
    };
}
