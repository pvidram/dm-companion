using System.Text;

namespace DmCompanion.Infrastructure.Tv;

/// <summary>Respuestas HTTP/1.1 mínimas; todas cierran la conexión salvo el canal de eventos.</summary>
internal static class TvHttpResponse
{
    public static Task WriteStatusAsync(Stream stream, int status, CancellationToken ct) =>
        WriteHeadAsync(stream, $"HTTP/1.1 {status} {Reason(status)}\r\n{(status == 405 ? "Allow: GET\r\n" : string.Empty)}Content-Length: 0\r\nConnection: close\r\n\r\n", ct);

    public static Task WriteRedirectAsync(Stream stream, string location, CancellationToken ct) =>
        WriteHeadAsync(stream, $"HTTP/1.1 301 Moved Permanently\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", ct);

    public static async Task WriteBodyAsync(Stream stream, string contentType, Stream body, CancellationToken ct)
    {
        var length = body.CanSeek ? $"Content-Length: {body.Length - body.Position}\r\n" : string.Empty;
        await WriteHeadAsync(stream,
            $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\n{length}Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n", ct);
        await body.CopyToAsync(stream, ct);
    }

    public static Task WriteEventStreamHeadAsync(Stream stream, CancellationToken ct) =>
        WriteHeadAsync(stream,
            "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-store\r\nConnection: keep-alive\r\n\r\nretry: 3000\n\n", ct);

    private static async Task WriteHeadAsync(Stream stream, string head, CancellationToken ct)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
        await stream.FlushAsync(ct);
    }

    private static string Reason(int status) => status switch
    {
        204 => "No Content",
        400 => "Bad Request",
        404 => "Not Found",
        405 => "Method Not Allowed",
        _ => "Internal Server Error",
    };
}
