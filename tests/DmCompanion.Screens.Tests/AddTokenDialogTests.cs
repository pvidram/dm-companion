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
        WriteNote("Bestiario/ogro.md", "---\ntype: monster\nname: Ogro\n---\n");
    }

    private async Task<IRenderedComponent<AddTokenDialog>> RenderAsync(MapState? state = null, string? rawState = null)
    {
        await OpenMapAsync(state, rawState, Notes);
        Shell.OpenAddToken();
        return Render<AddTokenDialog>();
    }

    private static AngleSharp.Dom.IElement ButtonText(IRenderedComponent<AddTokenDialog> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private static Task SearchAsync(IRenderedComponent<AddTokenDialog> cut, string text) =>
        cut.InvokeAsync(() => cut.Find("input[type=search]").Input(text));

    private static AngleSharp.Dom.IElement AddRow(IRenderedComponent<AddTokenDialog> cut, string name) =>
        cut.FindAll("[data-testid=token-result]").Single(r => r.GetAttribute("data-name") == name)
            .QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Añadir");

    private async Task<IRenderedComponent<AddTokenDialog>> RenderWithNotesAsync(params (string Path, string Content)[] extra)
    {
        await OpenMapAsync(null, null, () =>
        {
            Notes();
            foreach (var (path, content) in extra) WriteNote(path, content);
        });
        Shell.OpenAddToken();
        return Render<AddTokenDialog>();
    }

    [Fact]
    public async Task Adding_a_pc_note_with_alias_prefills_the_token_alias()
    {
        var cut = await RenderWithNotesAsync(("Personajes/lobo.md", "---\ntype: pc\nname: Lobo\nalias: El Lobo\n---\n"));
        await SearchAsync(cut, "Lobo");

        await cut.InvokeAsync(() => AddRow(cut, "Lobo").Click());

        Tokens.Single().Should().Match<MapToken>(t => t.Alias == "El Lobo" && t.Name == "Lobo");
    }

    [Fact]
    public async Task Adding_a_pc_note_without_alias_leaves_the_token_alias_null()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Borin");

        await cut.InvokeAsync(() => AddRow(cut, "Borin").Click());

        Tokens.Single().Alias.Should().BeNull();
    }

    [Fact]
    public async Task Alias_in_a_non_pc_note_is_ignored()
    {
        var cut = await RenderWithNotesAsync(("Bestiario/brujo.md", "---\ntype: npc\nname: Brujo\nalias: El Oscuro\n---\n"));
        await SearchAsync(cut, "Brujo");

        await cut.InvokeAsync(() => AddRow(cut, "Brujo").Click());

        Tokens.Single().Alias.Should().BeNull();
    }

    [Fact]
    public async Task Add_all_pcs_copies_each_alias()
    {
        var cut = await RenderWithNotesAsync(("Personajes/lobo.md", "---\ntype: pc\nname: Lobo\nalias: El Lobo\n---\n"));

        await cut.InvokeAsync(() => ButtonText(cut, "Añadir todos los PJ").Click());

        Tokens.OrderBy(t => t.Name).Select(t => (t.Name, t.Alias)).Should().Contain(("Lobo", "El Lobo")).And.Contain(("Borin", null));
    }

    [Fact]
    public async Task Adding_a_note_twice_numbers_the_second_token()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        AddRow(cut, "Goblin").Click();

        Tokens.Select(t => t.Name).Should().Equal("Goblin", "Goblin 2");
    }

    [Fact]
    public async Task Adding_a_note_twice_keeps_the_same_source()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        AddRow(cut, "Goblin").Click();

        Tokens.Select(t => t.Source).Should().Equal(GoblinNote, GoblinNote);
    }

    [Fact]
    public async Task Added_tokens_do_not_overlap()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Goblin");

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
        await SearchAsync(cut, "Goblin");

        AddRow(cut, "Goblin").Click();
        cut.WaitForState(() => Tokens.Count == 1, Timeout);

        (Tokens.Single().X, Tokens.Single().Y).Should().Be((500.0, 400.0));
    }

    [Fact]
    public async Task An_npc_note_defaults_to_the_enemy_side()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Goblin");

        AddRow(cut, "Goblin").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Enemy);
    }

    [Fact]
    public async Task A_pc_note_defaults_to_the_pc_side()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Borin");

        AddRow(cut, "Borin").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Pc);
    }

    [Fact]
    public async Task The_side_chosen_before_adding_is_used()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Goblin");
        var row = cut.Find("[data-testid=token-result][data-name=Goblin]");

        row.QuerySelector("select")!.Change("1"); // Pc, Ally, Enemy
        AddRow(cut, "Goblin").Click();

        Tokens.Single().Side.Should().Be(TokenSide.Ally);
    }

    [Fact]
    public async Task From_note_lists_monster_notes()
    {
        var cut = await RenderAsync();

        cut.FindAll("[data-testid=token-result]").Select(r => r.GetAttribute("data-name")).Should().Contain("Ogro");
    }

    [Fact]
    public async Task Search_only_offers_pc_and_npc_notes()
    {
        var cut = await RenderAsync();
        await SearchAsync(cut, "Cripta");

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

    // --- Desde el glosario ---

    private async Task<IRenderedComponent<AddTokenDialog>> RenderPrefilledAsync(TokenDraft draft)
    {
        await OpenMapAsync(null, null, Notes);
        Shell.OpenAddToken(draft);
        return Render<AddTokenDialog>();
    }

    private static readonly TokenDraft Aboleth = new("Aboleto", "srd:criaturas/aboleth", TokenSide.Enemy, 2, 17, 150);

    [Fact]
    public async Task Prefill_hides_the_tabs_and_shows_the_name_read_only()
    {
        var cut = await RenderPrefilledAsync(Aboleth);

        (cut.FindAll(".token-dialog__tabs").Count, cut.Find("[data-testid=token-prefill-name]").TextContent).Should().Be((0, "Aboleto"));
    }

    [Fact]
    public async Task Prefill_add_uses_the_chosen_side_and_size()
    {
        var cut = await RenderPrefilledAsync(Aboleth);

        Field(cut, "Bando").Change("1");
        Field(cut, "Tamaño").Change("2"); // Opciones 1, 2, 3: índice 2 = 3×3
        ButtonText(cut, "Añadir").Click();

        Tokens.Single().Should().Match<MapToken>(t => t.Side == TokenSide.Ally && t.Size == 3 && t.Source == "srd:criaturas/aboleth");
    }

    [Fact]
    public async Task Prefill_add_closes_the_dialog_and_clears_the_prefill()
    {
        var cut = await RenderPrefilledAsync(Aboleth);

        ButtonText(cut, "Añadir").Click();

        (Shell.AddTokenOpen, Shell.AddTokenPrefill).Should().Be((false, null));
    }

    [Fact]
    public async Task Prefill_view_has_the_Desde_el_glosario_heading()
    {
        var cut = await RenderPrefilledAsync(Aboleth);

        cut.Find("h3").TextContent.Should().Be("Desde el glosario");
    }

    [Fact]
    public async Task Closing_the_dialog_clears_the_prefill()
    {
        var cut = await RenderPrefilledAsync(Aboleth);

        ButtonText(cut, "Cerrar").Click();

        (Shell.AddTokenOpen, Shell.AddTokenPrefill).Should().Be((false, null));
    }

    private static string? SelectedOption(IRenderedComponent<AddTokenDialog> cut, string label) =>
        Field(cut, label).QuerySelectorAll("option").Single(o => o.HasAttribute("selected")).TextContent;

    [Fact]
    public async Task Opening_without_prefill_after_one_restores_the_defaults()
    {
        var cut = await RenderPrefilledAsync(Aboleth with { Size = 3 });
        Shell.CloseAddToken();

        Shell.OpenAddToken();
        ButtonText(cut, "Genérico").Click();

        (SelectedOption(cut, "Bando"), SelectedOption(cut, "Tamaño")).Should().Be(("Enemigo", "1×1"));
    }
}
