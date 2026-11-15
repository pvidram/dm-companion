using System.Net;
using System.Net.Http;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure.Tv;
using Microsoft.Extensions.Logging.Abstractions;
using static DmCompanion.Infrastructure.Tests.Tv.TvTestClient;

namespace DmCompanion.Infrastructure.Tests.Tv;

/// <summary>Suscriptores que fallan, clientes que dejan de leer y exceso de conexiones.</summary>
public sealed class TvServerRobustnessTests : IAsyncLifetime
{
    private const int FirstPort = 18890;
    private const string Code = "abcde23456";

    private readonly TvServer _server = new(new FakeTvAssets(), NullLogger<TvServer>.Instance, FirstPort, 10)
    {
        WriteTimeout = TimeSpan.FromSeconds(1),
        MaxConnections = 2,
    };
    private readonly HttpClient _http = new() { Timeout = Wait };
    private int _port;

    async Task IAsyncLifetime.InitializeAsync() => _port = (await _server.StartAsync(Code)).Port;

    async Task IAsyncLifetime.DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
    }

    private string Url(string path) => $"http://127.0.0.1:{_port}{path}";

    private Task<HttpResponseMessage> OpenEventsAsync() =>
        _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url($"/tv/{Code}/events")), HttpCompletionOption.ResponseHeadersRead);

    [Fact]
    public async Task Viewport_subscriber_that_throws_still_gets_204()
    {
        _server.ViewportReported += (_, _) => throw new InvalidOperationException();

        using var response = await _http.GetAsync(Url($"/tv/{Code}/size?w=800&h=600"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Connections_subscriber_that_throws_does_not_leave_a_ghost_connection()
    {
        _server.ConnectionsChanged += (_, _) => throw new InvalidOperationException();

        var response = await OpenEventsAsync();
        await WaitUntilAsync(() => _server.ConnectedCount == 1);
        response.Dispose();

        await WaitUntilAsync(() => _server.ConnectedCount == 0);
    }

    [Fact]
    public async Task Client_that_stops_reading_is_dropped()
    {
        using var stalled = await SendRawAsync(_port, $"/tv/{Code}/events");
        await WaitUntilAsync(() => _server.ConnectedCount == 1);

        // Mensajes grandes llenan los búferes; la escritura se bloquea y vence el plazo.
        var big = "\"" + new string('x', 8 * 1024 * 1024) + "\"";
        for (var i = 0; i < 3; i++) _server.Publish(big);

        await WaitUntilAsync(() => _server.ConnectedCount == 0);
    }

    [Fact]
    public async Task Connections_over_the_limit_are_closed()
    {
        using var first = await SendRawAsync(_port, $"/tv/{Code}/events");
        using var second = await SendRawAsync(_port, $"/tv/{Code}/events");
        await WaitUntilAsync(() => _server.ConnectedCount == 2);

        using var third = await SendRawAsync(_port, $"/tv/{Code}/");
        using var timeout = new CancellationTokenSource(Wait);
        var read = await ReadUntilClosedAsync(third, timeout.Token);

        read.Should().Be(0);
    }

    private static async Task<int> ReadUntilClosedAsync(System.Net.Sockets.TcpClient client, CancellationToken ct)
    {
        var buffer = new byte[1024];
        var total = 0;
        try
        {
            int read;
            while ((read = await client.GetStream().ReadAsync(buffer, ct)) > 0) total += read;
        }
        catch (IOException)
        {
            // Un RST también es «cerrada sin respuesta».
        }
        return total;
    }
}
