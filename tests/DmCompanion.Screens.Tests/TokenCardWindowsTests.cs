using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class TokenCardWindowsTests : TokenTestBase
{
    private async Task<IRenderedComponent<TokenCardLayer>> LayerAsync()
    {
        await OpenMapAsync(MapMapState());
        return Render<TokenCardLayer>();
    }

    private static MapState MapMapState() => MapState.Empty with
    {
        Tokens = [GenericToken("a", "Orco", hp: 10), GenericToken("b", "Trasgo", hp: 7)],
    };

    private static Task PinAsync(IRenderedComponent<TokenCardLayer> cut, string name) =>
        cut.InvokeAsync(() => cut.FindAll("section.ds-quick-card").First(c => c.TextContent.Contains(name)).QuerySelector("button[aria-label='Anclar']")!.Click());

    [Fact]
    public async Task Two_pinned_cards_render_at_once()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await PinAsync(cut, "Orco");
        Windows.OpenTokenCard("b", 50, 50);
        await PinAsync(cut, "Trasgo");

        cut.WaitForAssertion(() => cut.FindAll("section.ds-quick-card:not([hidden])").Should().HaveCount(2), Timeout);
    }

    [Fact]
    public async Task Tapping_another_creature_replaces_the_unpinned_card()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        Windows.OpenTokenCard("b", 50, 50);

        cut.WaitForAssertion(() => cut.FindAll("section.ds-quick-card").Select(c => c.QuerySelector("h3")!.TextContent).Should().Equal("Trasgo"), Timeout);
    }

    [Fact]
    public async Task Editing_card_survives_opening_another_creature()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Editar")).Click());
        Windows.OpenTokenCard("b", 50, 50);

        cut.WaitForAssertion(() => cut.FindAll("section.ds-quick-card").Should().HaveCount(2), Timeout);
    }

    [Fact]
    public async Task Removing_the_token_closes_its_pinned_card()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await PinAsync(cut, "Orco");

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Quitar del mapa")).Click());

        Windows.TokenCards.Should().BeEmpty();
    }

    [Fact]
    public async Task Pin_button_pins_the_card()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);

        await PinAsync(cut, "Orco");

        Windows.TokenCards.Should().ContainSingle().Which.IsPinned.Should().BeTrue();
    }

    private static int Layer(IRenderedComponent<TokenCardLayer> cut, string name)
    {
        var style = cut.FindAll("section.ds-quick-card").First(c => c.TextContent.Contains(name)).GetAttribute("style")!;
        return int.Parse(System.Text.RegularExpressions.Regex.Match(style, @"\+ (\d+)\)").Groups[1].Value);
    }

    [Fact]
    public async Task Later_cards_are_stacked_above()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await PinAsync(cut, "Orco");
        Windows.OpenTokenCard("b", 50, 50);

        cut.WaitForAssertion(() => Layer(cut, "Trasgo").Should().BeGreaterThan(Layer(cut, "Orco")), Timeout);
    }

    [Fact]
    public async Task Bringing_a_card_to_front_keeps_layers_dense_and_reapplies_its_frame()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await PinAsync(cut, "Orco");
        Windows.OpenTokenCard("b", 50, 50);
        cut.WaitForAssertion(() => cut.FindAll("section.ds-quick-card").Should().HaveCount(2), Timeout);
        var frames = Services.GetRequiredService<FakeWindowFrameInterop>();
        var before = frames.Updated.Count;

        for (var i = 0; i < 5; i++)
        {
            Windows.BringToFront("a");
            Windows.BringToFront("b");
        }
        Windows.BringToFront("a");

        cut.WaitForAssertion(() => (Layer(cut, "Orco"), Layer(cut, "Trasgo")).Should().Be((2, 1)), Timeout);
        frames.Updated.Count.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task Losing_the_editor_while_editing_clears_the_editing_flag()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Editar")).Click());
        Windows.TokenCards.Single().IsEditing.Should().BeTrue();

        await Session.CloseAsync();

        cut.WaitForAssertion(() => Windows.TokenCards.Where(c => c.TokenId == "a").Select(c => c.IsEditing).Should().NotContain(true), Timeout);
    }

    [Fact]
    public async Task A_focus_in_a_card_brings_it_to_front()
    {
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);
        await PinAsync(cut, "Orco");
        Windows.OpenTokenCard("b", 50, 50);
        cut.WaitForAssertion(() => cut.FindAll("section.ds-quick-card").Should().HaveCount(2), Timeout);

        await cut.InvokeAsync(() => cut.FindAll("section.ds-quick-card").First(c => c.TextContent.Contains("Orco")).TriggerEvent("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs()));

        Windows.TokenCards.Last().TokenId.Should().Be("a");
    }

    [Fact]
    public async Task A_stored_size_is_rendered_in_the_card_style_so_a_style_rewrite_keeps_it()
    {
        Services.GetRequiredService<DmCompanion.Screens.Services.PanelLayoutStore>().SetSize(DmCompanion.Screens.Services.LayoutKeys.TokenCard, new DmCompanion.UI.Interop.WindowSize(420, null));
        var cut = await LayerAsync();
        Windows.OpenTokenCard("a", 10, 10);

        cut.WaitForAssertion(() => cut.Find("section.ds-quick-card").GetAttribute("style").Should().Contain("width: 420px").And.NotContain("height"), Timeout);
    }
}
