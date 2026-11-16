using System.Globalization;
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

        var contentType = VaultFileRequest.ContentTypeOf(relative);
        var total = TryGetLength(vault, relative);
        var parse = RangeParse.None;
        var range = default(ByteRange);
        if (total is { } length) parse = ByteRanges.TryParse(RangeHeaderOf(e.Headers), length, out range);

        switch (parse)
        {
            case RangeParse.Ok:
                e.SetResponse(206, "Partial Content", new Dictionary<string, string>
                {
                    ["Content-Type"] = contentType,
                    ["Content-Range"] = ContentRange(range, total),
                    ["Content-Length"] = range.Length.ToString(CultureInfo.InvariantCulture),
                    ["Accept-Ranges"] = "bytes",
                }, OpenOrEmptyAsync(vault, relative, range));
                break;
            case RangeParse.Unsatisfiable:
                e.SetResponse(416, "Range Not Satisfiable", new Dictionary<string, string>
                {
                    ["Content-Range"] = ContentRange(null, total),
                    ["Accept-Ranges"] = "bytes",
                }, Task.FromResult<Stream?>(new MemoryStream()));
                break;
            default:
                var headers = new Dictionary<string, string> { ["Content-Type"] = contentType, ["Accept-Ranges"] = "bytes" };
                if (total is { } whole) headers["Content-Length"] = whole.ToString(CultureInfo.InvariantCulture);
                e.SetResponse(200, "OK", headers, OpenOrEmptyAsync(vault, relative, null));
                break;
        }
    }

    private static string ContentRange(ByteRange? range, long? total)
    {
        var size = (total ?? 0).ToString(CultureInfo.InvariantCulture);
        return range is { } r
            ? string.Create(CultureInfo.InvariantCulture, $"bytes {r.Start}-{r.End}/{size}")
            : $"bytes */{size}";
    }

    private static string? RangeHeaderOf(IEnumerable<KeyValuePair<string, string>>? headers) =>
        headers?.FirstOrDefault(h => string.Equals(h.Key, "Range", StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// <c>SetResponse</c> necesita el código de estado antes de que termine la tarea del cuerpo, así que el tamaño
    /// se lee aquí. El almacén de ficheros devuelve la tarea ya completada; si no lo está (o falla) se sirve el
    /// fichero entero sin bloquear el hilo de la petición.
    /// </summary>
    private long? TryGetLength(VaultService vault, string relative)
    {
        try
        {
            var stat = vault.StatAsync(relative);
            if (!stat.IsCompletedSuccessfully)
            {
                logger.LogWarning("El tamaño de un fichero del vault no estaba disponible al instante; se sirve entero sin rangos ({RelativePath}).", relative);
                return null;
            }
            // No bloquea: la tarea ya está completada y esto corre en el hilo de fondo de shouldInterceptRequest.
            return stat.GetAwaiter().GetResult()?.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning("No se pudo leer el tamaño de un fichero del vault ({ExceptionType}).", ex.GetType().FullName);
            return null;
        }
    }

    private async Task<Stream?> OpenOrEmptyAsync(VaultService vault, string relative, ByteRange? range)
    {
        try
        {
            var stream = await vault.OpenReadAsync(relative);
            return range is { } slice ? new LimitedReadStream(stream, slice.Start, slice.Length) : stream;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning("No se pudo abrir un fichero del vault ({ExceptionType}).", ex.GetType().FullName);
            return new MemoryStream();
        }
    }
}
