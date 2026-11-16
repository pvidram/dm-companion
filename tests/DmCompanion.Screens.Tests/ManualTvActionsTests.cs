using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Manuals;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class ManualTvActionsTests : ManualsUiTestBase
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 9];

    private readonly FakeTvServer _server = new();

    public ManualTvActionsTests()
    {
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<ITvServer>(_server);
        Services.AddSingleton<TvMapFeed>();
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    private TvSession Tv => Services.GetRequiredService<TvSession>();

    private readonly List<bool> _croppingChanges = [];

    private IRenderedComponent<ManualTvActions> Render(int page = 4)
    {
        var context = new ManualViewerContext(PdfPath, "Manual de prueba", page, new ManualDocInfo(12, [], true), [],
            Services.GetRequiredService<ManualViewerInterop>(), _ => Task.CompletedTask, SetCropping: _croppingChanges.Add);
        return Render<ManualTvActions>(p => p.AddCascadingValue(context));
    }

    private static IElement? Button(IRenderedComponent<ManualTvActions> cut, string label) =>
        cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    private void SetupPng(byte[]? png) =>
        Module.Setup<IJSStreamReference>("renderPng", _ => true).SetResult(new BytesStreamReference(png ?? []));

    [Fact]
    public void Tv_buttons_hidden_with_tv_off()
    {
        var cut = Render();

        (Button(cut, "Mostrar en TV"), Button(cut, "Recortar para la TV")).Should().Be((null, null));
    }

    [Fact]
    public async Task Show_page_renders_at_tv_size_and_sends()
    {
        await Tv.EnableAsync();
        SetupPng(Png);
        var cut = Render(page: 4);

        await cut.InvokeAsync(() => Button(cut, "Mostrar en TV")!.Click());

        cut.WaitForAssertion(() => (Tv.Mode, Tv.ContentTitle).Should().Be((TvMode.Content, "Manual de prueba · p. 4")), Wait);
        var args = Module.VerifyInvoke("renderPng").Arguments;
        (args[0], args[1], args[2], args[3]).Should().Be((4, null, 1920, 1080));
    }

    [Fact]
    public async Task Show_page_without_png_sends_nothing_and_warns()
    {
        await Tv.EnableAsync();
        Module.Setup<IJSStreamReference>("renderPng", _ => true).SetException(new JSException("fallo"));
        var cut = Render();

        await cut.InvokeAsync(() => Button(cut, "Mostrar en TV")!.Click());

        cut.WaitForAssertion(() => cut.Find("[role=alert]"), Wait);
        (Tv.Mode, _server.Files.Count).Should().Be((TvMode.Waiting, 0));
    }

    [Fact]
    public async Task Crop_cancel_sends_nothing()
    {
        await Tv.EnableAsync();
        var pick = Module.Setup<CropRect?>("pickCrop");
        var cut = Render();
        await cut.InvokeAsync(() => Button(cut, "Recortar para la TV")!.Click());
        cut.WaitForAssertion(() => Button(cut, "Cancelar").Should().NotBeNull(), Wait);

        await cut.InvokeAsync(() => Button(cut, "Cancelar")!.Click());
        pick.SetResult(null);

        cut.WaitForAssertion(() => Button(cut, "Cancelar").Should().BeNull(), Wait);
        (Module.Invocations["cancelCrop"].Count, Module.Invocations.Any(i => i.Identifier == "renderPng"), Tv.Mode).Should().Be((1, false, TvMode.Waiting));
    }

    [Fact]
    public async Task Crop_sends_cropped_png()
    {
        await Tv.EnableAsync();
        var crop = new CropRect(0.1, 0.2, 0.3, 0.4);
        var pick = Module.Setup<CropRect?>("pickCrop");
        SetupPng(Png);
        var cut = Render(page: 7);
        await cut.InvokeAsync(() => Button(cut, "Recortar para la TV")!.Click());
        cut.WaitForAssertion(() => Button(cut, "Enviar").Should().NotBeNull(), Wait);

        await cut.InvokeAsync(() => Button(cut, "Enviar")!.Click());
        pick.SetResult(crop);

        cut.WaitForAssertion(() => (Tv.Mode, Tv.ContentTitle).Should().Be((TvMode.Content, "Manual de prueba · p. 7")), Wait);
        var args = Module.VerifyInvoke("renderPng").Arguments;
        (Module.Invocations["confirmCrop"].Count, args[0], args[1], args[2], args[3]).Should().Be((1, 7, crop, 1920, 1080));
    }

    private async Task<IRenderedComponent<ManualTvActions>> StartCropAsync()
    {
        await Tv.EnableAsync();
        Module.Setup<CropRect?>("pickCrop");
        var cut = Render();
        await cut.InvokeAsync(() => Button(cut, "Recortar para la TV")!.Click());
        cut.WaitForAssertion(() => Button(cut, "Cancelar").Should().NotBeNull(), Wait);
        return cut;
    }

    [Fact]
    public async Task Crop_marks_the_viewer_as_cropping()
    {
        await StartCropAsync();

        _croppingChanges.Should().Equal(true);
    }

    [Fact]
    public async Task Ending_the_crop_clears_the_cropping_mark()
    {
        await Tv.EnableAsync();
        var pick = Module.Setup<CropRect?>("pickCrop");
        var cut = Render();
        await cut.InvokeAsync(() => Button(cut, "Recortar para la TV")!.Click());
        cut.WaitForAssertion(() => Button(cut, "Cancelar").Should().NotBeNull(), Wait);

        await cut.InvokeAsync(() => pick.SetResult(null));

        cut.WaitForAssertion(() => _croppingChanges.Should().Equal(true, false), Wait);
    }

    [Fact]
    public async Task Turning_the_tv_off_cancels_the_crop()
    {
        var cut = await StartCropAsync();

        await Tv.DisableAsync();

        cut.WaitForAssertion(() => Module.Invocations["cancelCrop"].Count.Should().Be(1), Wait);
    }

    [Fact]
    public async Task Unmounting_during_a_crop_cancels_it()
    {
        await StartCropAsync();

        await DisposeComponentsAsync();

        Module.Invocations["cancelCrop"].Count.Should().Be(1);
    }

    private sealed class BytesStreamReference(byte[] bytes) : IJSStreamReference
    {
        public long Length => bytes.Length;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
