using System.Text;

namespace DmCompanion.Infrastructure.Tv;

internal enum TvReadStatus { Ok, BadRequest, Closed }

/// <summary>Línea de petición HTTP/1.x. Las cabeceras se leen (hasta el límite) pero no se usan.</summary>
internal sealed record TvHttpRequest(string Method, string Path, string Query)
{
    public const int MaxHeadBytes = 8 * 1024;

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    /// <summary>Lee la cabecera completa; si supera <see cref="MaxHeadBytes"/> o no se entiende, BadRequest.</summary>
    public static async Task<(TvReadStatus Status, TvHttpRequest? Request)> ReadAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[MaxHeadBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), ct);
            if (read == 0) return (TvReadStatus.Closed, null);
            length += read;
            var end = buffer.AsSpan(0, length).IndexOf(HeadEnd);
            if (end >= 0)
            {
                var request = Parse(buffer.AsSpan(0, end));
                return request is null ? (TvReadStatus.BadRequest, null) : (TvReadStatus.Ok, request);
            }
        }
        return (TvReadStatus.BadRequest, null);
    }

    internal static TvHttpRequest? Parse(ReadOnlySpan<byte> head)
    {
        var lineEnd = head.IndexOf("\r\n"u8);
        var line = Encoding.ASCII.GetString(lineEnd >= 0 ? head[..lineEnd] : head);
        var parts = line.Split(' ');
        if (parts.Length != 3 || parts[0].Length == 0 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal)) return null;
        var target = parts[1];
        if (!target.StartsWith('/') || target.Any(c => c < 0x21 || c > 0x7E)) return null;
        var question = target.IndexOf('?');
        return question < 0
            ? new TvHttpRequest(parts[0], target, string.Empty)
            : new TvHttpRequest(parts[0], target[..question], target[(question + 1)..]);
    }

    /// <summary>Valor de un parámetro de la query, sin decodificar (solo se usan enteros).</summary>
    public string? QueryValue(string name)
    {
        foreach (var pair in Query.Split('&'))
        {
            var equals = pair.IndexOf('=');
            if (equals > 0 && pair.AsSpan(0, equals).SequenceEqual(name)) return pair[(equals + 1)..];
        }
        return null;
    }
}
