using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

/// <summary>URL virtual de un fichero del vault; el parámetro <c>v</c> (mtime) invalida la caché del navegador.</summary>
public sealed class VaultFileUrls(VaultHost host)
{
    public string? UrlFor(string relativePath, DateTime lastWriteUtc)
    {
        if (host.Current is null) return null;
        if (!VaultPaths.TryNormalizeSafe(VaultPaths.Normalize(relativePath), out var safe)) return null;
        var encoded = string.Join('/', safe.Split('/').Select(Uri.EscapeDataString));
        return $"{VaultFileRequest.Prefix}{encoded}?v={lastWriteUtc.Ticks}";
    }
}
