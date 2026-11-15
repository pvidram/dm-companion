using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class CombatActionsTests : CombatUiTestBase
{
    private static void Amount(IRenderedComponent<DmShell> cut, string text) => cut.Find("[data-testid=hp-amount]").Input(text);

    // --- HP ---

    [Fact]
    public async Task Damage_asks_for_a_positive_number()
    {
        var cut = await RenderAsync();

        Amount(cut, "0");
        cut.Find("[data-testid=hp-damage]").Click();

        cut.Find("[data-testid=hp-error]").TextContent.Should().Contain("Escribe un número entero positivo");
    }

    [Fact]
    public async Task Damage_subtracts_from_the_current_turn_token_when_nothing_is_selected()
    {
        var cut = await RenderAsync();

        Amount(cut, "6");
        cut.Find("[data-testid=hp-damage]").Click();

        Ana.HpCurrent.Should().Be(14);
    }

    [Fact]
    public async Task Damage_acts_on_the_selected_token()
    {
        var cut = await RenderAsync();
        cut.FindAll("[data-testid=strip-select]")[1].Click();

        Amount(cut, "6");
        cut.Find("[data-testid=hp-damage]").Click();

        Tokens.First(t => t.Id == "b").HpCurrent.Should().Be(14);
    }

    [Fact]
    public async Task Heal_adds_up_to_max()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20) with { HpCurrent = 15 });

        Amount(cut, "50");
        cut.Find("[data-testid=hp-heal]").Click();

        Ana.HpCurrent.Should().Be(20);
    }

    [Fact]
    public async Task Damage_is_disabled_without_current_and_max()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana"));

        cut.Find("[data-testid=hp-damage]").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Temp_hp_proposes_the_greater_value_and_replaces()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20) with { HpTemp = 5 });

        cut.Find("[data-testid=temp-input]").Input("3");
        cut.Find("[data-testid=temp-set]").Click();

        cut.Find("[data-testid=temp-confirm]").GetAttribute("value").Should().Be("5");
    }

    [Fact]
    public async Task Temp_hp_confirmed_value_replaces_the_current_one()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20) with { HpTemp = 5 });
        cut.Find("[data-testid=temp-input]").Input("3");
        cut.Find("[data-testid=temp-set]").Click();

        cut.Find("[data-testid=temp-confirm]").Input("3");
        cut.Find("[data-testid=temp-accept]").Click();

        Ana.HpTemp.Should().Be(3);
    }

    [Fact]
    public async Task Temp_hp_greater_than_current_applies_directly()
    {
        var cut = await RenderAsync();

        cut.Find("[data-testid=temp-input]").Input("8");
        cut.Find("[data-testid=temp-set]").Click();

        Ana.HpTemp.Should().Be(8);
    }

    // --- Ajustes de la revisión de la tarea 7 ---

    [Fact]
    public async Task Handle_does_not_start_drags_when_read_only()
    {
        await RenderAsync();
        var token = Tokens[0];

        var cut = Render<CombatStripRow>(p => p
            .Add(x => x.Entry, Combat.State!.Entries[0]).Add(x => x.Token, token).Add(x => x.CanEdit, false));

        cut.FindAll(".combat-strip__handle").Should().BeEmpty();
    }

    [Fact]
    public async Task Compact_cell_shows_the_name()
    {
        var cut = await RenderAsync();
        cut.Find("[data-testid=strip-toggle]").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid=strip-cell-name]").Select(n => n.TextContent).Should().Equal("Ana", "Bruno"));
    }

    [Fact]
    public async Task Initiative_field_keeps_the_value_when_typing_without_leaving_the_field()
    {
        var cut = await RenderAsync(start: true, more: GenericToken("c", "Cora", hp: 5));
        cut.Find("[data-testid=strip-add]").Click();

        cut.Find("[data-testid=add-initiative]").Input("12");
        cut.Find("[data-testid=add-token-c]").Click();

        Combat.State!.Entries.First(e => e.TokenId == "c").Initiative.Should().Be(12);
    }
}
