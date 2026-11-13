using DmCompanion.Core.Vault;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

/// <summary>Atiende las peticiones <c>/_vault/…</c> del WebView con ficheros del vault abierto.</summary>
internal sealed class VaultFileRequestHandler(VaultHost host, ILogger<VaultFileRequestHandler> logger)
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

    public void Handle(WebViewWebResourceRequestedEventArgs e)
    {
        // Uri.PathAndQuery conserva la codificación original; TryParse decodifica una sola vez.
        if (e.Uri is not { } uri || !uri.AbsolutePath.StartsWith(VaultFileRequest.Prefix, StringComparison.Ordinal)) return;

        e.Handled = true;
        if (host.Current is not { } vault || !VaultFileRequest.TryParse(uri.PathAndQuery, out var relative))
        {
            e.SetResponse(404, "Not Found", NoHeaders, Task.FromResult<Stream?>(new MemoryStream()));
            return;
        }
        e.SetResponse(200, "OK",
            new Dictionary<string, string> { ["Content-Type"] = VaultFileRequest.ContentTypeOf(relative) },
            OpenOrEmptyAsync(vault, relative));
    }

    private async Task<Stream?> OpenOrEmptyAsync(VaultService vault, string relative)
    {
        try { return await vault.OpenReadAsync(relative); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning("No se pudo abrir un fichero del vault ({ExceptionType}).", ex.GetType().FullName);
            return new MemoryStream();
        }
    }
}
