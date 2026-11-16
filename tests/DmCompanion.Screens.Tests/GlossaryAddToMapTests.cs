using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class GlossaryAddToMapTests : TokenTestBase
{
    private const string Aboleth = "srd:criaturas/aboleth";
    private const string GoblinWarrior = "srd:criaturas/goblin-warrior";

    private IRenderedComponent<AddCreatureToMapButton> Button(string key) =>
        Render<AddCreatureToMapButton>(p => p.Add(c => c.Key, key));

    private static AngleSharp.Dom.IElement AddButton(IRenderedComponent<AddCreatureToMapButton> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Añadir al mapa");

    [Fact]
    public async Task Add_to_map_is_hidden_without_an_open_editable_map()
    {
        await Host.OpenAsync(PathOf(string.Empty));

        Button(Aboleth).FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public async Task Add_to_map_is_hidden_for_a_non_creature_entry()
    {
        await OpenMapAsync();

        Button("srd:estados/blinded").FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public async Task Add_to_map_opens_the_dialog_prefilled_with_name_ac_hp_and_size()
    {
        await OpenMapAsync();

        AddButton(Button(Aboleth)).Click();

        Shell.AddTokenPrefill.Should().Be(new TokenDraft("Aboleth", Aboleth, TokenSide.Enemy, 2, 17, 150));
    }

    [Fact]
    public async Task Add_to_map_for_a_monster_note_prefills_title_and_path()
    {
        await OpenMapAsync(null, null, () => WriteNote("Bestiario/ogro.md", "---\ntype: monster\nname: Ogro\n---\n"));

        AddButton(Button("vault:Bestiario/ogro.md")).Click();

        Shell.AddTokenPrefill.Should().Be(new TokenDraft("Ogro", "Bestiario/ogro.md", TokenSide.Enemy, 1, null, null));
    }

    [Fact]
    public async Task Adding_two_goblins_numbers_them_and_both_point_to_srd()
    {
        await OpenMapAsync();
        var button = Button(GoblinWarrior);
        var dialog = Render<AddTokenDialog>();

        AddButton(button).Click();
        dialog.FindAll("button").First(b => b.TextContent.Trim() == "Añadir").Click();
        AddButton(button).Click();
        dialog.FindAll("button").First(b => b.TextContent.Trim() == "Añadir").Click();

        Tokens.Select(t => (t.Name, t.Source)).Should().Equal(("Guerrero goblin", GoblinWarrior), ("Guerrero goblin 2", GoblinWarrior));
    }
}
