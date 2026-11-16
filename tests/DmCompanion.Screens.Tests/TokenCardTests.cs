using System.Text.Json;
using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class TokenCardTests : TokenTestBase
{
    private const string GoblinNote = "Bestiario/goblin.md";
    private const string LyraWithStats = "---\ntype: pc\nname: Lyra\nstats:\n  ac: 14\n  hp: 27\n  speed: 30\n---\n";

    private static MapState StateOf(params MapToken[] tokens) => MapState.Empty with { Tokens = tokens };

    private async Task<IRenderedComponent<TokenCard>> CardAsync(MapToken token, string? lyra = null, Action? extraNotes = null, string? rawState = null)
    {
        await OpenMapAsync(StateOf(token), rawState, () =>
        {
            WriteNote(MapTestVault.LyraNote, lyra ?? LyraWithStats);
            WriteNote(GoblinNote, "---\ntype: npc\nname: Goblin\nstats:\n  ac: 15\n  hp: 7\n  speed: 30\n---\n");
            extraNotes?.Invoke();
        });
        Shell.OpenTokenCard(new TokenCardRequest(token.Id, 100, 100));
        return Render<TokenCard>();
    }

    private static string Stat(IRenderedComponent<TokenCard> cut, string label) =>
        cut.FindAll(".ds-stat").First(s => s.QuerySelector(".ds-stat__label")!.TextContent == label)
            .QuerySelector(".ds-stat__value")!.TextContent;

    private static string HitPointsRow(IRenderedComponent<TokenCard> cut) =>
        string.Join(' ', cut.Find("[data-testid=token-hp]").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static AngleSharp.Dom.IElement ButtonText(IRenderedComponent<TokenCard> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private static bool HasButton(IRenderedComponent<TokenCard> cut, string text) =>
        cut.FindAll("button").Any(b => b.TextContent.Trim() == text);

    [Fact]
    public async Task Card_shows_temp_hp()
    {
        var cut = await CardAsync(GenericToken("g", "Goblin", hp: 7) with { HpTemp = 5 });

        cut.Find("[data-testid=token-temp-hp]").TextContent.Should().Contain("5");
    }

    [Fact]
    public async Task Card_shows_conditions()
    {
        var condition = new DmCompanion.Core.Combat.TokenCondition("c1", "frightened", "Asustado", null, null, false);
        var cut = await CardAsync(GenericToken("g", "Goblin", hp: 7) with { Conditions = [condition] });

        cut.Find("[data-testid=token-conditions]").TextContent.Should().Contain("Asustado");
    }

    [Fact]
    public async Task Card_without_combat_data_shows_no_temp_hp_or_conditions()
    {
        var cut = await CardAsync(GenericToken("g", "Goblin", hp: 7));

        cut.FindAll("[data-testid=token-temp-hp], [data-testid=token-conditions]").Should().BeEmpty();
    }

    // --- Datos ---

    [Fact]
    public async Task Note_token_shows_the_armor_class_of_its_note()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));

        Stat(cut, "AC").Should().Be("14");
    }

    [Fact]
    public async Task Note_token_shows_quick_card_stats_in_system_order_with_hit_points_in_their_own_row()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));

        cut.FindAll(".ds-stat__label").Select(l => l.TextContent).Should().Equal("AC", "Velocidad", "Percepción pasiva");
    }

    [Fact]
    public async Task Note_token_shows_speed_and_its_hit_points_row()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));

        (Stat(cut, "Velocidad"), HitPointsRow(cut)).Should().Be(("30", "HP 27 / 27"));
    }

    [Fact]
    public async Task Missing_stat_in_the_note_shows_a_dash()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote), lyra: "---\ntype: pc\nname: Lyra\n---\n");

        Stat(cut, "AC").Should().Be("—");
    }

    [Fact]
    public async Task Card_shows_the_new_value_after_the_note_is_reindexed()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));

        await Host.Current!.SaveAsync(MapTestVault.LyraNote, LyraWithStats.Replace("ac: 14", "ac: 17"), WritePrecondition.Overwrite);

        cut.WaitForAssertion(() => Stat(cut, "AC").Should().Be("17"), Timeout);
    }

    [Fact]
    public async Task Cosmere_campaign_uses_that_systems_quick_card_stats()
    {
        // Cosmere (provisional) no marca ninguna stat como tarjeta rápida: la tarjeta no debe inventar las de D&D (solo la fila de Salud).
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote),
            extraNotes: () => WriteNote("Campana.md", "---\ntype: campaign\nname: Campaña\nsystem: cosmere\n---\n"));

        cut.FindAll(".ds-stat").Should().BeEmpty();
    }

    [Fact]
    public async Task Generic_token_shows_its_own_armor_class_and_hit_points_row()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", ac: 12, hp: 11));

        (Stat(cut, "AC"), HitPointsRow(cut)).Should().Be(("12", "HP 11 / 11"));
    }

    [Fact]
    public async Task Generic_token_without_armor_class_shows_a_dash()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", hp: 11));

        Stat(cut, "AC").Should().Be("—");
    }

    [Fact]
    public async Task Generic_token_shows_no_speed()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", ac: 12, hp: 11));

        cut.FindAll(".ds-stat__label").Select(l => l.TextContent).Should().Equal("AC");
    }

    [Fact]
    public async Task Card_shows_the_name_and_the_side()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", side: TokenSide.Ally));

        (cut.Find("h3").TextContent, cut.Find("[data-testid=token-side]").TextContent).Should().Be(("Orco", "Aliado"));
    }

    [Fact]
    public async Task Lost_note_shows_the_notice()
    {
        var cut = await CardAsync(NoteToken("t", "Fantasma", "Personajes/borrada.md"));

        cut.Markup.Should().Contain("Nota no encontrada");
    }

    [Fact]
    public async Task Lost_note_hides_see_more()
    {
        var cut = await CardAsync(NoteToken("t", "Fantasma", "Personajes/borrada.md"));

        HasButton(cut, "Ver más").Should().BeFalse();
    }

    [Fact]
    public async Task Generic_token_has_no_see_more()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        HasButton(cut, "Ver más").Should().BeFalse();
    }

    // --- Ver más ---

    [Fact]
    public async Task See_more_on_a_pc_selects_it_in_the_players_panel()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));

        ButtonText(cut, "Ver más").Click();

        (Shell.ActivePanel, Shell.SelectedPcPath).Should().Be((OverlayPanel.Players, MapTestVault.LyraNote));
    }

    [Fact]
    public async Task See_more_on_a_pc_keeps_the_players_panel_open_if_it_already_was()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));
        Shell.Toggle(OverlayPanel.Players);

        ButtonText(cut, "Ver más").Click();

        Shell.ActivePanel.Should().Be(OverlayPanel.Players);
    }

    [Fact]
    public async Task See_more_on_an_npc_opens_its_note()
    {
        var cut = await CardAsync(NoteToken("t", "Goblin", GoblinNote, TokenSide.Enemy));

        ButtonText(cut, "Ver más").Click();

        (Shell.ActivePanel, Shell.SelectedNotePath).Should().Be((OverlayPanel.Notes, GoblinNote));
    }

    [Fact]
    public async Task See_more_closes_the_card()
    {
        var cut = await CardAsync(NoteToken("t", "Goblin", GoblinNote, TokenSide.Enemy));

        ButtonText(cut, "Ver más").Click();

        Shell.TokenCard.Should().BeNull();
    }

    // --- Edición ---

    private static IEnumerable<string> FieldLabels(IRenderedComponent<TokenCard> cut) =>
        cut.FindAll("label.ds-field .ds-field__label").Select(l => l.TextContent);

    [Fact]
    public async Task Editing_a_note_token_offers_only_side_and_size()
    {
        var cut = await CardAsync(NoteToken("t", "Lyra", MapTestVault.LyraNote));

        ButtonText(cut, "Editar").Click();

        FieldLabels(cut).Should().Equal("Bando", "Tamaño", "Alias");
    }

    [Fact]
    public async Task Editing_a_generic_token_offers_name_armor_class_and_hit_points_too()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        ButtonText(cut, "Editar").Click();

        FieldLabels(cut).Should().Equal("Bando", "Tamaño", "Alias", "Nombre", "AC (opcional)", "HP (opcional)");
    }

    [Fact]
    public async Task Changing_the_side_to_ally_is_stored_in_the_state()
    {
        var cut = await CardAsync(NoteToken("t", "Goblin", GoblinNote, TokenSide.Enemy));
        ButtonText(cut, "Editar").Click();

        Field(cut, "Bando").Change("1"); // Pc, Ally, Enemy
        ButtonText(cut, "Guardar").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Ally);
    }

    [Fact]
    public async Task Changing_the_side_to_ally_reaches_the_canvas_dto_as_faction_ally()
    {
        var cut = await CardAsync(NoteToken("t", "Goblin", GoblinNote, TokenSide.Enemy));
        var interop = Services.GetRequiredService<MapCanvasInterop>();
        await interop.LoadAsync(default);
        ButtonText(cut, "Editar").Click();

        Field(cut, "Bando").Change("1");
        ButtonText(cut, "Guardar").Click();
        await interop.RenderAsync();

        var dto = JsonSerializer.SerializeToElement(JSInterop.Invocations["mapCanvas.render"].Last().Arguments[0], JsonSerializerOptions.Web);
        dto.GetProperty("tokens")[0].GetProperty("faction").GetString().Should().Be("ally");
    }

    [Fact]
    public async Task Saving_leaves_edit_mode()
    {
        var cut = await CardAsync(NoteToken("t", "Goblin", GoblinNote, TokenSide.Enemy));
        ButtonText(cut, "Editar").Click();

        ButtonText(cut, "Guardar").Click();

        HasButton(cut, "Guardar").Should().BeFalse();
    }

    [Fact]
    public async Task Editing_a_generic_token_stores_name_armor_class_and_hit_points()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));
        ButtonText(cut, "Editar").Click();

        Field(cut, "Nombre").Input("Orco jefe");
        Field(cut, "AC (opcional)").Input("16");
        Field(cut, "HP (opcional)").Input("30");
        Field(cut, "Tamaño").Change("2"); // 3×3
        ButtonText(cut, "Guardar").Click();

        Tokens.Single().Should().Match<MapToken>(t => t.Name == "Orco jefe" && t.Ac == 16 && t.Hp == 30 && t.Size == 3);
    }

    [Fact]
    public async Task Save_is_disabled_when_a_generic_name_is_blank()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));
        ButtonText(cut, "Editar").Click();

        Field(cut, "Nombre").Input("  ");

        ButtonText(cut, "Guardar").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_discards_the_edit()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));
        ButtonText(cut, "Editar").Click();
        Field(cut, "Nombre").Input("Otro");

        ButtonText(cut, "Cancelar").Click();

        Tokens.Single().Name.Should().Be("Orco");
    }

    // --- Quitar ---

    [Fact]
    public async Task Remove_deletes_the_token()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        ButtonText(cut, "Quitar del mapa").Click();

        Tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_closes_the_card()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        ButtonText(cut, "Quitar del mapa").Click();

        Shell.TokenCard.Should().BeNull();
    }

    // --- Solo lectura y cierre ---

    [Theory]
    [InlineData("Editar")]
    [InlineData("Quitar del mapa")]
    public async Task Read_only_map_hides_edit_and_remove(string text)
    {
        var cut = await ReadOnlyCardAsync();

        HasButton(cut, text).Should().BeFalse();
    }

    private async Task<IRenderedComponent<TokenCard>> ReadOnlyCardAsync()
    {
        await OpenMapAsync();
        // Un estado futuro (versión 3) abre el mapa en solo lectura con sus tokens a la vista.
        var json = "{\"version\":4,\"tokens\":[{\"id\":\"t\",\"name\":\"Orco\",\"side\":\"enemy\",\"size\":1,\"x\":35,\"y\":35}],\"strokes\":[]}";
        await Session.CloseAsync();
        File.WriteAllText(PathOf(MapPaths.StatePathFor(MapTestVault.MapNote)), json);
        await Session.OpenAsync(MapTestVault.MapNote);
        Session.IsReadOnly.Should().BeTrue();
        Shell.OpenTokenCard(new TokenCardRequest("t", 100, 100));
        return Render<TokenCard>();
    }

    [Fact]
    public async Task Card_is_hidden_when_there_is_no_request()
    {
        await OpenMapAsync();

        Render<TokenCard>().Find("section").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public async Task Card_is_hidden_when_the_token_no_longer_exists()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));
        Session.Mutate(e => e.RemoveToken("t"));

        cut.WaitForAssertion(() => cut.Find("section").HasAttribute("hidden").Should().BeTrue(), Timeout);
    }

    [Fact]
    public async Task Close_button_clears_the_request()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        cut.Find("button[aria-label='Cerrar']").Click();

        Shell.TokenCard.Should().BeNull();
    }

    [Fact]
    public async Task Card_is_placed_at_the_requested_position()
    {
        var cut = await CardAsync(GenericToken("t", "Orco"));

        cut.Find("section").GetAttribute("style").Should().Be("left: 100px; top: 100px;");
    }

    // --- Toque en un token ---

    private async Task<(IRenderedComponent<TokenCard> Cut, MapCanvasInterop Interop)> TappableAsync(double[]? hostRect)
    {
        JSInterop.Setup<double[]>("mapCanvas.hostRect").SetResult(hostRect!);
        var cut = await CardAsync(GenericToken("t", "Orco"));
        Shell.CloseTokenCard();
        var interop = Services.GetRequiredService<MapCanvasInterop>();
        await interop.LoadAsync(default);
        return (cut, interop);
    }

    [Fact]
    public async Task Tapping_a_token_opens_its_card_next_to_it_inside_the_map_area()
    {
        var (_, interop) = await TappableAsync([100, 50, 1000, 800]);

        interop.OnTokenTapped("t", 600, 450); // en el área del mapa: (500, 400)

        Shell.TokenCard.Should().Be(new TokenCardRequest("t", 532, 240));
    }

    [Fact]
    public async Task Tapping_an_unknown_token_does_nothing()
    {
        var (_, interop) = await TappableAsync([100, 50, 1000, 800]);

        interop.OnTokenTapped("otro", 600, 450);

        Shell.TokenCard.Should().BeNull();
    }

    [Fact]
    public async Task Card_header_is_the_drag_handle()
    {
        var cut = await CardAsync(GenericToken("g", "Goblin", hp: 7));

        cut.Find(".ds-quick-card > header").HasAttribute("data-drag-handle").Should().BeTrue();
    }

    [Fact]
    public async Task Opening_the_card_places_it_next_to_the_token_through_the_drag_service()
    {
        await CardAsync(GenericToken("g", "Goblin", hp: 7));

        JSInterop.Invocations["dragPanel.attach"].Single().Arguments.Skip(1).Should().Equal(100d, 100d);
    }

    [Fact]
    public async Task Opening_the_card_again_places_it_again()
    {
        var cut = await CardAsync(GenericToken("g", "Goblin", hp: 7));
        Shell.OpenTokenCard(new TokenCardRequest("g", 100, 100));
        cut.Render();

        JSInterop.Invocations["dragPanel.attach"].Should().HaveCount(2);
    }

    [Fact]
    public async Task Rerendering_the_same_open_card_does_not_reset_a_dragged_position()
    {
        var cut = await CardAsync(GenericToken("g", "Goblin", hp: 7));
        cut.Render();

        JSInterop.Invocations["dragPanel.attach"].Should().HaveCount(1);
    }
}
