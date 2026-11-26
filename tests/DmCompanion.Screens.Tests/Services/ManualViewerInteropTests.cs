using Bunit;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests.Services;

public sealed class ManualViewerInteropTests : BunitContext
{
    private const string Url = "/_vault/manuales/dnd5e/Manual%20de%20prueba.pdf";

    private readonly BunitJSModuleInterop _module;

    public ManualViewerInteropTests()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        _module = JSInterop.SetupModule(ManualViewerInterop.ModulePath);
        _module.SetupVoid("close", _ => true).SetVoidResult();
    }

    [Fact]
    public async Task Open_passes_the_url_and_page()
    {
        _module.Setup<ManualDocInfo?>("open", _ => true).SetResult(new ManualDocInfo(3, [], true));
        await using var interop = CreateInterop();

        await interop.OpenAsync(new ElementReference("host"), Url, 7);

        _module.VerifyInvoke("open").Arguments.Skip(1).Take(2).Should().Equal(Url, 7);
    }

    [Fact]
    public async Task Open_returns_null_on_js_error_and_logs_only_type()
    {
        _module.Setup<ManualDocInfo?>("open", _ => true).SetException(new JSException("texto secreto del manual"));
        var logger = new TextLogger();
        await using var interop = new ManualViewerInterop(JSInterop.JSRuntime, logger);

        var info = await interop.OpenAsync(new ElementReference("host"), Url, 1);

        new { info, logger.Entries }.Should().BeEquivalentTo(new
        {
            info = (ManualDocInfo?)null,
            Entries = new[] { "No se pudo abrir el manual (Microsoft.JSInterop.JSException)." },
        });
    }

    [Theory]
    [InlineData("https://example.com/manual.pdf")]
    [InlineData("/_content/DmCompanion.Screens/manual.pdf")]
    public async Task Open_rejects_urls_outside_the_vault_without_calling_js(string url)
    {
        await using var interop = CreateInterop();

        var info = await interop.OpenAsync(new ElementReference("host"), url, 1);

        new { info, Calls = _module.Invocations.Count }.Should().BeEquivalentTo(new { info = (ManualDocInfo?)null, Calls = 0 });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowPage_returns_whether_the_page_was_painted(bool painted)
    {
        _module.Setup<bool>("showPage", _ => true).SetResult(painted);
        await using var interop = CreateInterop();

        var result = await interop.ShowPageAsync(4);

        result.Should().Be(painted);
    }

    [Fact]
    public async Task ShowPage_returns_false_on_js_error()
    {
        _module.Setup<bool>("showPage", _ => true).SetException(new JSException("x"));
        await using var interop = CreateInterop();

        var result = await interop.ShowPageAsync(4);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Close_disposes_on_DisposeAsync()
    {
        _module.Setup<ManualDocInfo?>("open", _ => true).SetResult(new ManualDocInfo(3, [], true));
        var interop = CreateInterop();
        await interop.OpenAsync(new ElementReference("host"), Url, 1);

        await interop.DisposeAsync();

        _module.VerifyInvoke("close");
    }

    [Fact]
    public async Task Search_forwards_hits_and_stops_on_cancel()
    {
        var planned = _module.Setup<bool>("search", _ => true);
        await using var interop = CreateInterop();
        var hits = new List<ManualSearchHit>();
        using var cts = new CancellationTokenSource();

        var searching = interop.SearchAsync("dragón", hit => { hits.Add(hit); return Task.CompletedTask; }, cts.Token);
        var session = ((DotNetObjectReference<ManualSearchSession>)planned.Invocations.Single().Arguments[1]!).Value;
        await session.OnHitAsync(2, "un dragón rojo");
        await cts.CancelAsync();
        var cancelledSeenByJs = session.IsCancelled();
        await session.OnHitAsync(3, "otro dragón");
        planned.SetResult(false);
        var completed = await searching;

        new { hits, cancelledSeenByJs, completed }.Should().BeEquivalentTo(new
        {
            hits = new[] { new ManualSearchHit(2, "un dragón rojo") },
            cancelledSeenByJs = true,
            completed = false,
        });
    }

    [Fact]
    public async Task Render_png_reads_the_bytes_from_the_stream()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        var crop = new CropRect(0.1, 0.2, 0.3, 0.4);
        _module.Setup<IJSStreamReference>("renderPng", inv => Equals(inv.Arguments[1], crop)).SetResult(new BytesStreamReference(png));
        await using var interop = CreateInterop();

        var bytes = await interop.RenderPngAsync(4, crop, 1920, 1080);

        bytes.Should().Equal(png);
    }

    [Fact]
    public async Task Page_changed_from_js_raises_PageChanged()
    {
        var planned = _module.Setup<ManualDocInfo?>("open", _ => true);
        planned.SetResult(new ManualDocInfo(3, [], true));
        await using var interop = CreateInterop();
        var pages = new List<int>();
        interop.PageChanged += pages.Add;
        await interop.OpenAsync(new ElementReference("host"), Url, 1);

        ((DotNetObjectReference<ManualViewerCallbacks>)planned.Invocations.Single().Arguments[3]!).Value.OnPageChanged(2);

        pages.Should().Equal(2);
    }

    [Fact]
    public async Task Page_failure_from_js_is_logged_by_type_only()
    {
        var planned = _module.Setup<ManualDocInfo?>("open", _ => true);
        planned.SetResult(new ManualDocInfo(3, [], true));
        var logger = new TextLogger();
        await using var interop = new ManualViewerInterop(JSInterop.JSRuntime, logger);
        await interop.OpenAsync(new ElementReference("host"), Url, 1);

        ((DotNetObjectReference<ManualViewerCallbacks>)planned.Invocations.Single().Arguments[3]!).Value.OnPageFailed("RenderingError");

        logger.Entries.Should().Equal("No se pudo pintar una página del manual (RenderingError).");
    }

    private ManualViewerInterop CreateInterop() => new(JSInterop.JSRuntime, NullLogger<ManualViewerInterop>.Instance);

    private sealed class BytesStreamReference(byte[] bytes) : IJSStreamReference
    {
        public long Length => bytes.Length;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TextLogger : ILogger<ManualViewerInterop>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(exception is null ? formatter(state, exception) : "excepción adjunta");
    }
}
