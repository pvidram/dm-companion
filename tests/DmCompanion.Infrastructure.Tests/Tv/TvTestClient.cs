using System.Net;
using System.Net.Sockets;
using System.Text;
using DmCompanion.Core.Tv;

namespace DmCompanion.Infrastructure.Tests.Tv;

/// <summary>Utilidades de cliente para probar <c>TvServer</c> por la red real (127.0.0.1).</summary>
internal static class TvTestClient
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    public static async Task<string> ReadDataAsync(StreamReader reader)
    {
        using var timeout = new CancellationTokenSource(Wait);
        while (true)
        {
            var line = await reader.ReadLineAsync(timeout.Token) ?? throw new EndOfStreamException();
            if (line.StartsWith("data:", StringComparison.Ordinal)) return line["data:".Length..].Trim();
        }
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(20);
        }
    }

    public static async Task<bool> CanConnectAsync(int port)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static TvFile TextFile(string text) =>
        new("text/plain", _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text))));

    /// <summary>Conexión a mano que manda una petición GET y no lee nada.</summary>
    public static async Task<TcpClient> SendRawAsync(int port, string target, string extraHeaders = "")
    {
        var client = new TcpClient { ReceiveBufferSize = 4096 };
        await client.ConnectAsync(IPAddress.Loopback, port);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"GET {target} HTTP/1.1\r\nHost: tv\r\n{extraHeaders}\r\n"));
        return client;
    }

    // HttpClient normaliza «..» y rechaza algunas cabeceras: estas peticiones van a mano.
    public static async Task<int> RawStatusAsync(int port, string target, string extraHeaders = "")
    {
        using var client = await SendRawAsync(port, target, extraHeaders);
        using var reader = new StreamReader(client.GetStream(), Encoding.ASCII);
        using var timeout = new CancellationTokenSource(Wait);
        var statusLine = await reader.ReadLineAsync(timeout.Token) ?? string.Empty;
        return int.Parse(statusLine.Split(' ')[1]);
    }
}
