using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

public enum PortraitStatus { None, Ok, TooLarge, Missing }

public sealed record PortraitResult(string? DataUrl, PortraitStatus Status);

// Singleton compartido entre vaults: la clave lleva la raíz del vault activo, así un cambio de vault
// nunca devuelve la imagen de otro. Una entrada por (raíz, ruta): al cambiar el mtime se sustituye, no se acumula.
public sealed class PortraitUrlProvider(VaultHost host, ILogger<PortraitUrlProvider> logger)
{
    public const long MaxBytes = 512 * 1024;

    private readonly ConcurrentDictionary<(string Root, string Path), CacheEntry> _cache = new();

    private sealed record CacheEntry(DateTime LastWriteTimeUtc, string DataUrl);

    public async Task<PortraitResult> GetAsync(string? portraitPath)
    {
        if (string.IsNullOrWhiteSpace(portraitPath)) return new PortraitResult(null, PortraitStatus.None);
        if (host.Current is not { } vault) return Missing;
        if (MimeOf(portraitPath) is not { } mime) return Missing;

        try
        {
            if (await vault.StatAsync(portraitPath) is not { } info) return Missing;
            // Se descarta por tamaño sin leer los bytes: nunca se envía un binario grande al circuito.
            if (info.Length > MaxBytes) return new PortraitResult(null, PortraitStatus.TooLarge);

            var key = (vault.RootPath, portraitPath);
            if (_cache.TryGetValue(key, out var cached) && cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
                return new PortraitResult(cached.DataUrl, PortraitStatus.Ok);

            var bytes = await vault.ReadBytesAsync(portraitPath);
            if (bytes.Length > MaxBytes) return new PortraitResult(null, PortraitStatus.TooLarge);

            var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            _cache[key] = new CacheEntry(info.LastWriteTimeUtc, dataUrl);
            return new PortraitResult(dataUrl, PortraitStatus.Ok);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "No se pudo leer el retrato «{Path}»; se muestra sin retrato.", portraitPath);
            return Missing;
        }
    }

    private static PortraitResult Missing => new(null, PortraitStatus.Missing);

    private static string? MimeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => null,
    };
}
