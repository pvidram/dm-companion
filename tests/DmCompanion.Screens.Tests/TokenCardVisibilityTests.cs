using System.Text.Json;
using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class TokenCardVisibilityTests : TokenTestBase
{
    public TokenCardVisibilityTests()
    {
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<WikilinkInterop>();
    }

    private async Task<IRenderedComponent<TokenCardLayer>> CardAsync(MapToken token, params MapToken[] more)
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [token, .. more] });
        Windows.OpenTokenCard(token.Id, 100, 100);
        return Render<TokenCardLayer>();
    }

    private static AngleSharp.Dom.IElement ButtonText(IRenderedComponent<TokenCardLayer> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private static bool HasButton(IRenderedComponent<TokenCardLayer> cut, string text) =>
        cut.FindAll("button").Any(b => b.TextContent.Trim() == text);

    [Fact]
    public async Task Hide_button_hides_token_and_becomes_reveal()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        ButtonText(cut, "Ocultar a los jugadores").Click();

        (Tokens.Single().IsHidden, HasButton(cut, "Revelar"), HasButton(cut, "Ocultar a los jugadores")).Should().Be((true, true, false));
    }

    [Fact]
    public async Task Reveal_button_shows_token()
    {
        var cut = await CardAsync(GenericToken("t", "Orco") with { Hidden = true });

        ButtonText(cut, "Revelar").Click();

        Tokens.Single().IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task Hide_is_undoable_from_history()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));
        ButtonText(cut, "Ocultar a los jugadores").Click();

        Session.Mutate(e => e.Undo());

        Tokens.Single().IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task Read_only_map_has_no_hide_button()
    {
        await OpenMapAsync();
        // Un estado futuro (versión futura) abre el mapa en solo lectura con sus tokens a la vista.
        var json = "{\"version\":5,\"tokens\":[{\"id\":\"t\",\"name\":\"Orco\",\"side\":\"enemy\",\"size\":1,\"x\":35,\"y\":35}],\"strokes\":[]}";
        await Session.CloseAsync();
        File.WriteAllText(PathOf(MapPaths.StatePathFor(MapTestVault.MapNote)), json);
        await Session.OpenAsync(MapTestVault.MapNote);
        Windows.OpenTokenCard("t", 100, 100);
        var cut = Render<TokenCardLayer>();

        (HasButton(cut, "Ocultar a los jugadores"), HasButton(cut, "Revelar")).Should().Be((false, false));
    }

    [Fact]
    public async Task Edit_form_saves_alias()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));
        ButtonText(cut, "Editar").Click();

        Field(cut, "Alias").Input("Figura encapuchada");
        ButtonText(cut, "Guardar").Click();

        Tokens.Single().Alias.Should().Be("Figura encapuchada");
    }

    [Fact]
    public async Task Edit_form_clears_alias()
    {
        var cut = await CardAsync(GenericToken("t", "Orco") with { Alias = "Sombra" });
        ButtonText(cut, "Editar").Click();

        Field(cut, "Alias").Input("  ");
        ButtonText(cut, "Guardar").Click();

        Tokens.Single().Alias.Should().BeNull();
    }

    [Fact]
    public async Task Editing_other_fields_keeps_the_alias()
    {
        var cut = await CardAsync(GenericToken("t", "Orco") with { Alias = "Sombra" });
        ButtonText(cut, "Editar").Click();

        Field(cut, "Nombre").Input("Orco jefe");
        ButtonText(cut, "Guardar").Click();

        Tokens.Single().Should().Match<MapToken>(t => t.Name == "Orco jefe" && t.Alias == "Sombra");
    }

    [Fact]
    public async Task Card_shows_alias_under_name_when_set()
    {
        var cut = await CardAsync(GenericToken("t", "Orco") with { Alias = "Sombra" });

        cut.Find("[data-testid=token-alias]").TextContent.Should().Contain("Sombra");
    }

    [Fact]
    public async Task Card_shows_no_alias_line_without_alias()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        cut.FindAll("[data-testid=token-alias]").Should().BeEmpty();
    }

    [Fact]
    public async Task Hidden_reaches_the_canvas_dto()
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [GenericToken("t", "Orco") with { Hidden = true }] });
        var interop = Services.GetRequiredService<MapCanvasInterop>();
        await interop.LoadAsync(default);

        var dto = JsonSerializer.SerializeToElement(JSInterop.Invocations["mapCanvas.load"].Last().Arguments[1], JsonSerializerOptions.Web);
        dto.GetProperty("tokens")[0].GetProperty("hidden").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Strip_avatar_is_hidden_for_hidden_token()
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [GenericToken("a", "Ana", hp: 20) with { Hidden = true }, GenericToken("b", "Bruno", hp: 20)] });
        Services.GetRequiredService<CombatController>().Start([new DmCompanion.Core.Combat.InitiativeEntry("a", 15), new DmCompanion.Core.Combat.InitiativeEntry("b", 10)]);
        var cut = Render<DmShell>();

        cut.FindAll("[data-testid=strip-row]").Select(r => r.QuerySelector(".ds-token--hidden") is not null).Should().Equal(true, false);
    }
}
