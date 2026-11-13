using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class AddTokenDialogTests : TokenTestBase
{
    private const string GoblinNote = "Bestiario/goblin.md";
    private const string BorinNote = "Personajes/borin.md";

    private void Notes()
    {
        WriteNote(GoblinNote, "---\ntype: npc\nname: Goblin\n---\n");
        WriteNote(BorinNote, "---\ntype: pc\nname: Borin\n---\n");
    }

    private async Task<IRenderedComponent<AddTokenDialog>> RenderAsync(MapState? state = null, string? rawState = null)
    {
        await OpenMapAsync(state, rawState, Notes);
        Shell.OpenAddToken();
        return Render<AddTokenDialog>();
    }

    private static AngleSharp.Dom.IElement ButtonText(IRenderedComponent<AddTokenDialog> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private static void Search(IRenderedComponent<AddTokenDialog> cut, string text) =>
        cut.Find("input[type=search]").Input(text);

    private static AngleSharp.Dom.IElement AddRow(IRenderedComponent<AddTokenDialog> cut, string name) =>
        cut.FindAll("[data-testid=token-result]").Single(r => r.GetAttribute("data-name") == name)
            .QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Añadir");

    [Fact]
    public async Task Adding_a_note_twice_numbers_the_second_token()
    {
        var cut = await RenderAsync();
        Search(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        AddRow(cut, "Goblin").Click();

        Tokens.Select(t => t.Name).Should().Equal("Goblin", "Goblin 2");
    }

    [Fact]
    public async Task Adding_a_note_twice_keeps_the_same_source()
    {
        var cut = await RenderAsync();
        Search(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        AddRow(cut, "Goblin").Click();

        Tokens.Select(t => t.Source).Should().Equal(GoblinNote, GoblinNote);
    }

    [Fact]
    public async Task Added_tokens_do_not_overlap()
    {
        var cut = await RenderAsync();
        Search(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        AddRow(cut, "Goblin").Click();

        Tokens.Select(t => (t.X, t.Y)).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Token_is_placed_at_the_center_of_the_view()
    {
        JSInterop.Setup<double[]>("mapCanvas.viewCenter").SetResult([500, 400]);
        var cut = await RenderAsync();
        await Services.GetRequiredService<MapCanvasInterop>().LoadAsync(default);
        Search(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        cut.WaitForState(() => Tokens.Count == 1, Timeout);

        (Tokens.Single().X, Tokens.Single().Y).Should().Be((500.0, 400.0));
    }

    [Fact]
    public async Task An_npc_note_defaults_to_the_enemy_side()
    {
        var cut = await RenderAsync();
        Search(cut, "Goblin");

        AddRow(cut, "Goblin").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Enemy);
    }

    [Fact]
    public async Task A_pc_note_defaults_to_the_pc_side()
    {
        var cut = await RenderAsync();
        Search(cut, "Borin");

        AddRow(cut, "Borin").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Pc);
    }

    [Fact]
    public async Task The_side_chosen_before_adding_is_used()
    {
        var cut = await RenderAsync();
        Search(cut, "Goblin");
        var row = cut.Find("[data-testid=token-result][data-name=Goblin]");

        row.QuerySelector("select")!.Change("1"); // Pc, Ally, Enemy
        AddRow(cut, "Goblin").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Ally);
    }

    [Fact]
    public async Task Search_only_offers_pc_and_npc_notes()
    {
        var cut = await RenderAsync();
        Search(cut, "Cripta");

        cut.FindAll("[data-testid=token-result]").Should().BeEmpty();
    }

    [Fact]
    public async Task Add_all_pcs_adds_only_those_without_a_token()
    {
        var cut = await RenderAsync(MapState.Empty with { Tokens = [NoteToken("t2", "Lyra", MapTestVault.LyraNote)] });

        ButtonText(cut, "Añadir todos los PJ").Click();

        Tokens.Select(t => t.Name).Should().Equal("Lyra", "Borin");
    }

    [Fact]
    public async Task Add_all_pcs_is_disabled_when_every_pc_is_on_the_map()
    {
        var cut = await RenderAsync(MapState.Empty with
        {
            Tokens = [NoteToken("t1", "Lyra", MapTestVault.LyraNote), NoteToken("t2", "Borin", BorinNote, x: 105)],
        });

        ButtonText(cut, "Añadir todos los PJ").HasAttribute("disabled").Should().BeTrue();
    }

    private static void GenericTab(IRenderedComponent<AddTokenDialog> cut) => ButtonText(cut, "Genérico").Click();

    [Fact]
    public async Task Generic_add_is_disabled_without_a_name()
    {
        var cut = await RenderAsync();
        GenericTab(cut);

        ButtonText(cut, "Añadir").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Generic_token_stores_ac_and_hp()
    {
        var cut = await RenderAsync();
        GenericTab(cut);

        Field(cut, "Nombre").Input("Orco");
        Field(cut, "AC (opcional)").Input("12");
        Field(cut, "HP (opcional)").Input("11");
        ButtonText(cut, "Añadir").Click();

        Tokens.Single().Should().Match<MapToken>(t => t.Name == "Orco" && t.Source == null && t.Ac == 12 && t.Hp == 11);
    }

    [Fact]
    public async Task Generic_token_without_ac_and_hp_leaves_them_empty()
    {
        var cut = await RenderAsync();
        GenericTab(cut);

        Field(cut, "Nombre").Input("Orco");
        ButtonText(cut, "Añadir").Click();

        (Tokens.Single().Ac, Tokens.Single().Hp).Should().Be((null, null));
    }

    [Fact]
    public async Task Generic_token_takes_the_chosen_size()
    {
        var cut = await RenderAsync();
        GenericTab(cut);

        Field(cut, "Nombre").Input("Ogro");
        Field(cut, "Tamaño").Change("1"); // 1×1, 2×2, 3×3
        ButtonText(cut, "Añadir").Click();

        Tokens.Single().Size.Should().Be(2);
    }

    [Fact]
    public async Task Generic_names_are_numbered_like_note_names()
    {
        var cut = await RenderAsync();
        GenericTab(cut);

        for (var i = 0; i < 2; i++)
        {
            Field(cut, "Nombre").Input("Orco");
            ButtonText(cut, "Añadir").Click();
        }

        Tokens.Select(t => t.Name).Should().Equal("Orco", "Orco 2");
    }

    [Fact]
    public async Task Read_only_map_offers_no_way_to_add()
    {
        var cut = await RenderAsync(rawState: "esto no es json");

        cut.FindAll("button").Select(b => b.TextContent.Trim()).Should().NotContain(["Añadir", "Añadir todos los PJ"]);
    }

    [Fact]
    public async Task Closing_the_dialog_clears_the_request()
    {
        var cut = await RenderAsync();

        ButtonText(cut, "Cerrar").Click();

        Shell.AddTokenOpen.Should().BeFalse();
    }
}
