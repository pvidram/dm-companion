using System.Net;
using System.Net.Sockets;
using System.Text;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure.Tv;
using Microsoft.Extensions.Logging.Abstractions;

using static DmCompanion.Infrastructure.Tests.Tv.TvTestClient;

namespace DmCompanion.Infrastructure.Tests.Tv;

public sealed class TvServerTests : IAsyncLifetime
{
    // Fuera del rango real (8590–8599) para no chocar con una app abierta en la máquina.
    private const int FirstPort = 18590;
    private const string Code = "brumavela";

    private readonly TvServer _server = new(new FakeTvAssets(), NullLogger<TvServer>.Instance, FirstPort, 10);
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = Wait };
    private int _port;

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
    }

    private async Task StartAsync() => _port = (await _server.StartAsync(Code)).Port;

    private string Url(string path) => $"http://127.0.0.1:{_port}{path}";

    private Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync(Url(path));

    private async Task<HttpStatusCode> StatusAsync(string path)
    {
        using var response = await GetAsync(path);
        return response.StatusCode;
    }

    private async Task<HttpResponseMessage> OpenEventsAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Url($"/tv/{Code}/events"));
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    [Fact]
    public async Task Not_listening_before_start_and_after_stop()
    {
        var before = await CanConnectAsync(FirstPort);
        await StartAsync();
        var during = await CanConnectAsync(_port);
        await _server.StopAsync();
        var after = await CanConnectAsync(_port);

        (before, during, after, _server.IsRunning).Should().Be((false, true, false, false));
    }

    [Fact]
    public async Task Index_is_served_under_the_code()
    {
        await StartAsync();

        using var response = await GetAsync($"/tv/{Code}/");

        (response.StatusCode, await response.Content.ReadAsStringAsync()).Should().Be((HttpStatusCode.OK, FakeTvAssets.IndexHtml));
    }

    [Fact]
    public async Task Wrong_code_is_still_404()
    {
        await StartAsync();

        (await StatusAsync("/tv/zzzzz23456/")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Code_matches_ignoring_case()
    {
        await StartAsync();

        (await StatusAsync("/tv/BrumaVela/")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Uppercase_code_given_to_the_server_still_matches()
    {
        _port = (await _server.StartAsync("BRUMAVELA")).Port;

        (await StatusAsync("/tv/brumavela/")).Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/tv")]
    [InlineData("/tv/")]
    [InlineData("/tv/" + Code + "/nada")]
    [InlineData("/tv/" + Code + "/assets/no-existe.js")]
    [InlineData("/tv/" + Code + "/events/x")]
    public async Task Unknown_route_is_404(string path)
    {
        await StartAsync();

        (await StatusAsync(path)).Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/tv/" + Code + "/files/../x")]
    [InlineData("/tv/" + Code + "/files/%2e%2e%2fx")]
    [InlineData("/tv/" + Code + "/assets/../../x")]
    [InlineData("/tv/" + Code + "/assets/..%2f..%2findex.html")]
    public async Task Path_traversal_is_404(string path)
    {
        await StartAsync();
        _server.SetFiles(new Dictionary<string, TvFile> { ["x"] = TextFile("secreto") });

        (await RawStatusAsync(path)).Should().Be(404);
    }

    [Fact]
    public async Task Post_is_405()
    {
        await StartAsync();

        using var response = await _http.PostAsync(Url($"/tv/{Code}/"), new StringContent("x"));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Oversized_headers_is_400_and_server_keeps_serving()
    {
        await StartAsync();

        var oversized = await RawStatusAsync($"/tv/{Code}/", "X-Big: " + new string('a', 9000) + "\r\n");
        var next = await StatusAsync($"/tv/{Code}/");

        (oversized, next).Should().Be((400, HttpStatusCode.OK));
    }

    [Fact]
    public async Task Index_redirects_without_trailing_slash()
    {
        await StartAsync();

        using var response = await GetAsync($"/tv/{Code}");

        (response.StatusCode, response.Headers.Location?.OriginalString).Should().Be((HttpStatusCode.MovedPermanently, $"/tv/{Code}/"));
    }

    [Fact]
    public async Task Events_send_last_message_on_connect()
    {
        await StartAsync();
        _server.Publish("""{"kind":"waiting","campaign":"A"}""");
        _server.Publish("""{"kind":"waiting","campaign":"B"}""");

        using var response = await OpenEventsAsync();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        (response.Content.Headers.ContentType?.MediaType, await ReadDataAsync(reader))
            .Should().Be(("text/event-stream", """{"kind":"waiting","campaign":"B"}"""));
    }

    [Fact]
    public async Task Events_receive_later_publish()
    {
        await StartAsync();
        using var response = await OpenEventsAsync();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        await WaitUntilAsync(() => _server.ConnectedCount == 1);

        _server.Publish("""{"kind":"waiting","campaign":"C"}""");

        (await ReadDataAsync(reader)).Should().Be("""{"kind":"waiting","campaign":"C"}""");
    }

    [Fact]
    public async Task Reconnect_receives_latest_message()
    {
        await StartAsync();
        _server.Publish("""{"n":1}""");
        using (var first = await OpenEventsAsync())
        {
            using var reader = new StreamReader(await first.Content.ReadAsStreamAsync());
            await ReadDataAsync(reader);
        }
        _server.Publish("""{"n":2}""");

        using var second = await OpenEventsAsync();
        using var secondReader = new StreamReader(await second.Content.ReadAsStreamAsync());

        (await ReadDataAsync(secondReader)).Should().Be("""{"n":2}""");
    }

    [Fact]
    public async Task ConnectedCount_tracks_open_event_streams()
    {
        await StartAsync();
        var changes = 0;
        _server.ConnectionsChanged += (_, _) => Interlocked.Increment(ref changes);

        var response = await OpenEventsAsync();
        await WaitUntilAsync(() => _server.ConnectedCount == 1);
        response.Dispose();
        await WaitUntilAsync(() => _server.ConnectedCount == 0);

        Volatile.Read(ref changes).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Size_raises_viewport()
    {
        await StartAsync();
        TvViewport? reported = null;
        _server.ViewportReported += (_, viewport) => reported = viewport;

        var status = await StatusAsync($"/tv/{Code}/size?w=1920&h=1080");

        (status, reported).Should().Be((HttpStatusCode.NoContent, new TvViewport(1920, 1080)));
    }

    [Fact]
    public async Task Size_with_invalid_values_is_400()
    {
        await StartAsync();

        (await StatusAsync($"/tv/{Code}/size?w=abc&h=1080")).Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Files_serves_only_whitelisted_ids()
    {
        await StartAsync();
        _server.SetFiles(new Dictionary<string, TvFile> { ["3"] = TextFile("mapa") });

        using var allowed = await GetAsync($"/tv/{Code}/files/3");
        var other = await StatusAsync($"/tv/{Code}/files/4");

        (allowed.StatusCode, await allowed.Content.ReadAsStringAsync(), other)
            .Should().Be((HttpStatusCode.OK, "mapa", HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Files_removed_from_whitelist_are_404()
    {
        await StartAsync();
        _server.SetFiles(new Dictionary<string, TvFile> { ["3"] = TextFile("mapa") });
        _server.SetFiles(new Dictionary<string, TvFile>());

        (await StatusAsync($"/tv/{Code}/files/3")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Falls_back_to_next_port_when_busy()
    {
        const int first = 18690;
        using var busy = new TcpListener(IPAddress.Any, first);
        busy.Start();
        await using var server = new TvServer(new FakeTvAssets(), NullLogger<TvServer>.Instance, first, 10);

        var endpoint = await server.StartAsync(Code);

        endpoint.Port.Should().Be(first + 1);
    }

    [Fact]
    public async Task Start_throws_io_exception_when_all_ports_are_busy()
    {
        const int first = 18790;
        using var busy1 = new TcpListener(IPAddress.Any, first);
        using var busy2 = new TcpListener(IPAddress.Any, first + 1);
        busy1.Start();
        busy2.Start();
        await using var server = new TvServer(new FakeTvAssets(), NullLogger<TvServer>.Instance, first, 2);

        var start = () => server.StartAsync(Code);

        await start.Should().ThrowAsync<IOException>();
    }

    private Task<int> RawStatusAsync(string target, string extraHeaders = "") => TvTestClient.RawStatusAsync(_port, target, extraHeaders);
}
