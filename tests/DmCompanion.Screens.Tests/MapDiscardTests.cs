using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class MapDiscardTests : MapsTestContext
{
    private async Task<IRenderedComponent<DmShell>> RenderWithFailedSaveAsync()
    {
        // Un directorio en el sitio del .state.json hace que el guardado falle siempre con IOException.
        Directory.CreateDirectory(PathOf("Mapas/Cueva.state.json"));
        var cut = Render<DmShell>();
        await Session.OpenAsync("Mapas/Cueva.md");
        Session.Mutate(e => e.AddToken(new TokenDraft("Orco", null, TokenSide.Enemy, 1, null, null), 5, 5) is not null);
        await Session.FlushAsync();
        cut.WaitForAssertion(() => cut.Find(".map-canvas__notices"), Wait);
        return cut;
    }

    private static AngleSharp.Dom.IElement DiscardButton(IRenderedComponent<DmShell> cut) =>
        cut.Find(".map-canvas__notices").QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Descartar cambios del mapa");

    [Fact]
    public async Task Failed_save_notice_offers_to_discard_the_map_changes()
    {
        var cut = await RenderWithFailedSaveAsync();

        DiscardButton(cut).Should().NotBeNull();
    }

    [Fact]
    public async Task Discard_asks_for_a_destructive_confirmation_before_doing_anything()
    {
        var cut = await RenderWithFailedSaveAsync();

        DiscardButton(cut).Click();

        cut.WaitForAssertion(() => (Shell.DiscardingMap, Session.Current is not null,
            OpenDialog(cut).QuerySelector("button.ds-button--damage")!.TextContent.Trim()).Should().Be((true, true, "Descartar cambios")), Wait);
    }

    [Fact]
    public async Task Confirming_the_discard_closes_the_map_and_clears_the_notice()
    {
        var cut = await RenderWithFailedSaveAsync();
        DiscardButton(cut).Click();
        cut.WaitForAssertion(() => OpenDialog(cut), Wait);

        OpenDialog(cut).QuerySelector("button.ds-button--damage")!.Click();

        cut.WaitForAssertion(() => (Session.Current, Shell.DiscardingMap, cut.FindAll(".map-canvas__notices").Count).Should().Be((null, false, 0)), Wait);
    }

    [Fact]
    public async Task Cancelling_the_discard_keeps_the_map_open()
    {
        var cut = await RenderWithFailedSaveAsync();
        DiscardButton(cut).Click();
        cut.WaitForAssertion(() => OpenDialog(cut), Wait);

        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Cancelar").Click();

        cut.WaitForAssertion(() => (Session.Current is not null, Shell.DiscardingMap).Should().Be((true, false)), Wait);
    }
}
