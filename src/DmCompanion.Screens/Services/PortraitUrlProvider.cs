using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

public enum PortraitStatus { None, Ok, Missing }

public sealed record PortraitResult(string? Url, PortraitStatus Status);

// El retrato se sirve por URL virtual (/_vault/…): no se lee ni se envía al circuito, así que no hay límite de tamaño.
// El ?v=mtime de la URL renueva la caché del navegador cuando el fichero cambia.
public sealed class PortraitUrlProvider(VaultHost host, VaultFileUrls urls, ILogger<PortraitUrlProvider> logger)
{
    public async Task<PortraitResult> GetAsync(string? portraitPath)
    {
        if (string.IsNullOrWhiteSpace(portraitPath)) return new PortraitResult(null, PortraitStatus.None);
        if (host.Current is not { } vault) return Missing;
        // Una ruta con «/» inicial (frontmatter escrito a mano) es válida: se normaliza antes de validarla.
        portraitPath = Core.Vault.VaultPaths.Normalize(portraitPath);
        if (!Core.Maps.MapPaths.IsImage(portraitPath)) return Missing;

        try
        {
            if (await vault.StatAsync(portraitPath) is not { } info) return Missing;
            return urls.UrlFor(portraitPath, info.LastWriteTimeUtc) is { } url
                ? new PortraitResult(url, PortraitStatus.Ok)
                : Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning("No se pudo leer un retrato; se muestra sin retrato ({ExceptionType}).", ex.GetType().Name);
            return Missing;
        }
    }

    private static PortraitResult Missing => new(null, PortraitStatus.Missing);
}
