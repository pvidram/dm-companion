using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class TvPanelTests : TvPanelTestBase
{
    [Fact]
    public void Rail_button_toggles_tv_panel()
    {
        var cut = RenderWithPanelOpen();

        cut.Find("button[aria-label='TV']").Click();

        cut.WaitForAssertion(() => Panel(cut).HasAttribute("hidden").Should().BeTrue(), Wait);
    }

    [Fact]
    public void Off_shows_the_activate_button()
    {
        var cut = RenderWithPanelOpen();

        Panel(cut).QuerySelectorAll("button").Select(b => b.TextContent.Trim()).Should().Contain("Activar TV");
    }

    [Fact]
    public void Panel_shows_address_qr_and_connection_state()
    {
        var cut = RenderEnabled();
        var address = Tv.AddressFor("192.168.1.20");

        cut.WaitForAssertion(() =>
        {
            var panel = Panel(cut);
            (panel.QuerySelector(".tv-panel__address")?.TextContent.Trim(),
             panel.QuerySelector("img.tv-panel__qr")?.GetAttribute("src")?.StartsWith("data:image/png;base64,"),
             panel.QuerySelector(".tv-panel__status")?.TextContent.Trim())
                .Should().Be((address, true, "Sin TV conectada"));
        }, Wait);

        _server.SetConnected(1);

        cut.WaitForAssertion(() => Panel(cut).QuerySelector(".tv-panel__status")!.TextContent.Trim().Should().Be("TV conectada"), Wait);
    }

    [Fact]
    public void Two_connected_tvs_are_counted()
    {
        var cut = RenderEnabled();

        _server.SetConnected(2);

        cut.WaitForAssertion(() => Panel(cut).QuerySelector(".tv-panel__status")!.TextContent.Trim().Should().Be("2 TVs conectadas"), Wait);
    }

    [Fact]
    public void Other_addresses_are_listed_as_text()
    {
        var cut = RenderEnabled();

        cut.WaitForAssertion(() => Panel(cut).QuerySelectorAll(".tv-panel__other li").Select(li => li.TextContent.Trim())
            .Should().Equal(Tv.AddressFor("10.0.0.5")), Wait);
    }

    [Fact]
    public void Panel_shows_ports_busy_error()
    {
        _server.PortsBusy = true;
        var cut = RenderWithPanelOpen();

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Activar TV").Click());

        cut.WaitForAssertion(() => Panel(cut).TextContent.Should().Contain("No se pudo activar la TV: puertos ocupados."), Wait);
    }

    [Fact]
    public void Panel_shows_a_generic_error_for_other_failures()
    {
        _server.StartFailure = new UnauthorizedAccessException();
        var cut = RenderWithPanelOpen();

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Activar TV").Click());

        cut.WaitForAssertion(() => Panel(cut).TextContent.Should().Contain("No se pudo activar la TV."), Wait);
    }

    [Fact]
    public void Deactivate_turns_the_tv_off()
    {
        var cut = RenderEnabled();

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Desactivar TV").Click());

        cut.WaitForAssertion(() => (Tv.Mode, _server.IsRunning).Should().Be((TvMode.Off, false)), Wait);
    }

    private static IElement? ToolbarFixButton(IRenderedComponent<DmShell> cut) =>
        cut.FindAll(".map-controls button[aria-label='Fijar encuadre de TV']").FirstOrDefault();

    [Fact]
    public void Fix_frame_button_needs_an_open_map()
    {
        var cut = RenderEnabled();

        Panel(cut).QuerySelectorAll("button").Select(b => b.TextContent.Trim()).Should().NotContain("Fijar encuadre de TV");
    }

    [Fact]
    public async Task Fixing_from_the_panel_shows_the_map_on_the_tv()
    {
        JSInterop.Setup<double[]?>("mapCanvas.viewRect").SetResult([0, 0, 900, 900]);
        var cut = RenderEnabled();
        await cut.InvokeAsync(() => Maps.OpenAsync(Cave));
        cut.WaitForAssertion(() => ButtonIn(Panel(cut), "Fijar encuadre de TV").Should().NotBeNull(), Wait);

        await cut.InvokeAsync(() => ButtonIn(Panel(cut), "Fijar encuadre de TV").Click());

        cut.WaitForAssertion(() => (Tv.Mode, Panel(cut).QuerySelector(".tv-panel__showing")?.TextContent.Trim())
            .Should().Be((TvMode.Map, "En la TV: mapa Boceto de la cueva")), Wait);
    }

    [Fact]
    public async Task Map_toolbar_offers_fixing_the_frame_only_with_the_tv_on()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Maps.OpenAsync(Cave));
        cut.WaitForAssertion(() => cut.FindAll(".map-controls").Should().NotBeEmpty(), Wait);
        ToolbarFixButton(cut).Should().BeNull();

        await cut.InvokeAsync(Tv.EnableAsync);

        cut.WaitForAssertion(() => ToolbarFixButton(cut).Should().NotBeNull(), Wait);
    }

    [Fact]
    public async Task Tablet_marks_the_tv_frame_on_the_tv_map()
    {
        JSInterop.Setup<double[]?>("mapCanvas.viewRect").SetResult([0, 0, 900, 900]);
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Maps.OpenAsync(Cave));
        await cut.InvokeAsync(Tv.EnableAsync);
        cut.WaitForAssertion(() => ToolbarFixButton(cut).Should().NotBeNull(), Wait);

        await cut.InvokeAsync(() => ToolbarFixButton(cut)!.Click());

        cut.WaitForAssertion(() => JSInterop.Invocations.Where(i => i.Identifier == "mapCanvas.setTvFrame").LastOrDefault().Arguments?[0]
            .Should().Be(new TvFrame(-350, 0, 1600, 900)), Wait);
    }
}
