using System.Globalization;
using DmCompanion.Core.Tv;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Infrastructure.Tv;

// Enrutado y respuestas de cada ruta de la TV.
public sealed partial class TvServer
{
    private async Task RouteAsync(TvHttpRequest request, Stream stream, CancellationToken ct)
    {
        var route = TvRoute.Match(request.Path, _code);
        if (route.Kind == TvRouteKind.NotFound)
        {
            await TvHttpResponse.WriteStatusAsync(stream, 404, ct);
            return;
        }
        if (request.Method != "GET")
        {
            await TvHttpResponse.WriteStatusAsync(stream, 405, ct);
            return;
        }
        var serving = route.Kind switch
        {
            TvRouteKind.RedirectToIndex => TvHttpResponse.WriteRedirectAsync(stream, $"/tv/{_code}/", ct),
            TvRouteKind.Index => ServeAssetAsync("index.html", stream, ct),
            TvRouteKind.Asset => ServeAssetAsync(route.Name, stream, ct),
            TvRouteKind.File => ServeFileAsync(route.Name, stream, ct),
            TvRouteKind.Size => ServeSizeAsync(request, stream, ct),
            _ => ServeEventsAsync(stream, ct),
        };
        await serving;
    }

    private async Task ServeAssetAsync(string name, Stream stream, CancellationToken ct)
    {
        await using var asset = _assets.Open(name);
        if (asset is null) await TvHttpResponse.WriteStatusAsync(stream, 404, ct);
        else await TvHttpResponse.WriteBodyAsync(stream, _assets.ContentType(name), asset, ct);
    }

    private async Task ServeFileAsync(string id, Stream stream, CancellationToken ct)
    {
        TvFile? file;
        lock (_gate) file = _files.GetValueOrDefault(id);
        if (file is null)
        {
            await TvHttpResponse.WriteStatusAsync(stream, 404, ct);
            return;
        }
        Stream content;
        try
        {
            content = await file.Open(ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("No se pudo abrir un fichero para la TV ({ExceptionType}).", ex.GetType().FullName);
            await TvHttpResponse.WriteStatusAsync(stream, 404, ct);
            return;
        }
        await using (content) await TvHttpResponse.WriteBodyAsync(stream, file.ContentType, content, ct);
    }

    private async Task ServeSizeAsync(TvHttpRequest request, Stream stream, CancellationToken ct)
    {
        if (!TryDimension(request.QueryValue("w"), out var width) || !TryDimension(request.QueryValue("h"), out var height))
        {
            await TvHttpResponse.WriteStatusAsync(stream, 400, ct);
            return;
        }
        // Antes del 204: cuando la TV recibe la respuesta, el tamaño ya se conoce.
        RaiseViewportReported(new TvViewport(width, height));
        await TvHttpResponse.WriteStatusAsync(stream, 204, ct);
    }

    private static bool TryDimension(string? value, out int dimension) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out dimension) && dimension is > 0 and <= 20_000;

    private async Task ServeEventsAsync(Stream stream, CancellationToken ct)
    {
        await TvHttpResponse.WriteEventStreamHeadAsync(stream, ct);
        var events = new TvEventStream(stream, WriteTimeout);
        try
        {
            lock (_gate)
            {
                _streams.Add(events);
                if (_last is not null) events.Enqueue(_last);
            }
            RaiseConnectionsChanged();
            await events.RunAsync(PingInterval, ct);
        }
        finally
        {
            bool removed;
            lock (_gate) removed = _streams.Remove(events);
            if (removed) RaiseConnectionsChanged();
        }
    }
}
