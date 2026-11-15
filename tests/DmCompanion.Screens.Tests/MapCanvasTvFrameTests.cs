using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class MapCanvasTvFrameTests : IAsyncLifetime
{
    private static readonly TvFrame Frame = new(-350, 0, 1600, 900);

    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly AnsweringJs _js = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly MapCanvasInterop _interop;

    public MapCanvasTvFrameTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        var urls = new VaultFileUrls(_host);
        _interop = new MapCanvasInterop(_js, _session, urls, _host,
            new PortraitUrlProvider(_host, urls, NullLogger<PortraitUrlProvider>.Instance), new ShellState(), NullLogger<MapCanvasInterop>.Instance);
        MapTestVault.WriteCanvasMap(_vault);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _host.OpenAsync(_vault.Root);
        await _session.OpenAsync(MapTestVault.MapNote);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private List<object?> FramesSent() => _js.Calls.Where(c => c.Identifier == "mapCanvas.setTvFrame").Select(c => c.Args[0]).ToList();

    [Fact]
    public async Task Frame_set_before_loading_is_drawn_once_loaded()
    {
        await _interop.SetTvFrameAsync(Frame);

        await _interop.LoadAsync(default);

        FramesSent().Should().Equal(Frame);
    }

    [Fact]
    public async Task Same_frame_is_not_sent_twice()
    {
        await _interop.LoadAsync(default);

        await _interop.SetTvFrameAsync(Frame);
        await _interop.SetTvFrameAsync(Frame with { });

        FramesSent().Should().HaveCount(1);
    }

    [Fact]
    public async Task Clearing_the_frame_sends_null()
    {
        await _interop.LoadAsync(default);
        await _interop.SetTvFrameAsync(Frame);

        await _interop.SetTvFrameAsync(null);

        FramesSent().Should().Equal(Frame, null);
    }

    [Fact]
    public async Task Visible_rect_comes_from_the_canvas_in_map_coordinates()
    {
        await _interop.LoadAsync(default);
        _js.ViewRect = [100, 50, 500, 300];

        (await _interop.VisibleRectAsync()).Should().Be(new TvFrame(100, 50, 500, 300));
    }

    [Fact]
    public async Task Visible_rect_without_area_is_null()
    {
        await _interop.LoadAsync(default);
        _js.ViewRect = [100, 50, 0, 300];

        (await _interop.VisibleRectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Visible_rect_is_null_without_a_loaded_canvas()
    {
        _js.ViewRect = [100, 50, 500, 300];

        (await _interop.VisibleRectAsync()).Should().BeNull();
    }

    private sealed class AnsweringJs : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];
        public double[]? ViewRect { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(identifier == "mapCanvas.viewRect" ? (TValue)(object?)ViewRect! : default!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
