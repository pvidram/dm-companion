using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class CombatStripTests : TokenTestBase
{
    public CombatStripTests()
    {
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<WikilinkInterop>();
    }

    private CombatController Combat => Services.GetRequiredService<CombatController>();
    private TurnStripInterop Interop => Services.GetRequiredService<TurnStripInterop>();

    private static readonly MapToken Hurt = GenericToken("a", "Ana", hp: 7) with { HpCurrent = 4, HpTemp = 5 };

    private static MapState Tokens3(params MapToken[] first) => MapState.Empty with
    {
        Tokens = [.. first, GenericToken("b", "Bruno", hp: 20), GenericToken("c", "Cora", hp: 20), GenericToken("d", "Dani", hp: 20)],
    };

    private async Task<IRenderedComponent<DmShell>> RenderCombatAsync(MapState? state = null, bool start = true)
    {
        await OpenMapAsync(state ?? Tokens3(GenericToken("a", "Ana", hp: 20)));
        if (start) Combat.Start([new InitiativeEntry("a", 15), new InitiativeEntry("b", 10), new InitiativeEntry("c", 5)]);
        return Render<DmShell>();
    }

    private string EntryOf(string tokenId) => Combat.State!.Entries.First(e => e.TokenId == tokenId).Id;

    private static IReadOnlyList<IElement> Rows(IRenderedComponent<DmShell> cut) => cut.FindAll("[data-testid=strip-row]");

    private static string[] Names(IRenderedComponent<DmShell> cut) =>
        [.. Rows(cut).Select(r => r.QuerySelector("[data-testid=strip-name]")!.TextContent)];

    [Fact]
    public async Task Strip_is_hidden_without_combat()
    {
        var cut = await RenderCombatAsync(start: false);

        cut.FindAll("[data-testid=combat-strip]").Should().BeEmpty();
    }

    [Fact]
    public async Task Expanded_strip_shows_round_and_one_row_per_entry_in_order()
    {
        var cut = await RenderCombatAsync();

        ($"{cut.Find("[data-testid=strip-round]").TextContent}: {string.Join(", ", Names(cut))}").Should().Be("Ronda 1: Ana, Bruno, Cora");
    }

    [Fact]
    public async Task Row_shows_initiative_name_hp_and_temp()
    {
        var cut = await RenderCombatAsync(Tokens3(Hurt));
        var row = Rows(cut)[0];

        new[] { row.QuerySelector("[data-testid=strip-initiative]")!.TextContent, row.QuerySelector("[data-testid=strip-hp]")!.TextContent, row.QuerySelector("[data-testid=strip-temp]")!.TextContent }
            .Should().Equal("15", "4/7", "+5");
    }

    [Fact]
    public async Task Row_without_known_max_shows_dash()
    {
        var cut = await RenderCombatAsync(Tokens3(GenericToken("a", "Ana")));

        Rows(cut)[0].QuerySelector("[data-testid=strip-hp]")!.TextContent.Should().Be("—");
    }

    [Fact]
    public async Task Current_entry_is_highlighted()
    {
        var cut = await RenderCombatAsync();
        Combat.NextTurn();

        cut.WaitForAssertion(() => Rows(cut).Select(r => r.ClassList.Contains("combat-strip__row--current")).Should().Equal(false, true, false));
    }

    [Fact]
    public async Task Down_token_row_is_dimmed()
    {
        var cut = await RenderCombatAsync(Tokens3(GenericToken("a", "Ana", hp: 7) with { HpCurrent = 0 }));

        Rows(cut)[0].ClassList.Should().Contain("combat-strip__row--down");
    }

    [Fact]
    public async Task Next_button_advances_the_turn()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=next-turn]").Click();

        Combat.State!.CurrentEntryId.Should().Be(EntryOf("b"));
    }

    [Fact]
    public async Task Collapsed_strip_sits_inside_the_frame_header()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-toggle]").Click();

        cut.FindAll("header.ds-frame__strip .combat-strip--collapsed").Should().ContainSingle();
    }

    [Fact]
    public async Task Collapsed_strip_is_not_over_the_map()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-toggle]").Click();

        cut.FindAll(".map-layout .combat-strip--collapsed").Should().BeEmpty();
    }

    [Fact]
    public async Task Expanded_strip_stays_in_the_map_layout()
    {
        var cut = await RenderCombatAsync();

        cut.FindAll(".map-layout > .combat-strip--expanded").Should().ContainSingle();
    }

    [Fact]
    public async Task Expanded_strip_is_not_in_the_frame_header()
    {
        var cut = await RenderCombatAsync();

        cut.FindAll("header.ds-frame__strip .combat-strip").Should().BeEmpty();
    }

    [Fact]
    public async Task Collapsed_strip_next_turn_is_a_small_icon_button()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-toggle]").Click();

        cut.Find("[data-testid=next-turn]").ClassList.Should().Contain("ds-icon-button");
    }

    [Fact]
    public async Task Collapsed_strip_next_button_is_named_by_its_aria_label()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-toggle]").Click();

        cut.Find("[data-testid=next-turn]").GetAttribute("aria-label").Should().Be("Siguiente turno");
    }

    [Fact]
    public async Task Toggle_collapses_and_expands_without_losing_state()
    {
        var cut = await RenderCombatAsync();
        Combat.NextTurn();

        cut.Find("[data-testid=strip-toggle]").Click();
        var collapsed = cut.FindAll("[data-testid=strip-cell]").Count;
        cut.Find("[data-testid=strip-toggle]").Click();

        (collapsed, Rows(cut).Count, Combat.State!.CurrentEntryId).Should().Be((3, 3, EntryOf("b")));
    }

    [Fact]
    public async Task Compact_strip_shows_dot_for_expired_condition()
    {
        var expired = new TokenCondition("k1", null, "Bendición", null, null, true);
        var cut = await RenderCombatAsync(Tokens3(GenericToken("a", "Ana", hp: 20) with { Conditions = [expired] }));

        cut.Find("[data-testid=strip-toggle]").Click();

        cut.FindAll("[data-testid=strip-cell] [data-testid=expired-dot]").Should().ContainSingle();
    }

    [Fact]
    public async Task Tapping_compact_cell_opens_the_token_card()
    {
        var cut = await RenderCombatAsync();
        cut.Find("[data-testid=strip-toggle]").Click();

        cut.FindAll("[data-testid=strip-cell]")[1].Click();

        Shell.TokenCard!.TokenId.Should().Be("b");
    }

    [Fact]
    public async Task Tapping_the_last_of_many_compact_cells_opens_the_card_inside_the_map_area()
    {
        JSInterop.Setup<double[]>("mapCanvas.hostRect").SetResult([0, 64, 1000, 736]);
        var state = MapState.Empty with { Tokens = [.. Enumerable.Range(0, 12).Select(i => GenericToken("t" + i, "Token " + i, hp: 20))] };
        await OpenMapAsync(state);
        Combat.Start([.. Enumerable.Range(0, 12).Select(i => new InitiativeEntry("t" + i, 20 - i))]);
        var cut = Render<DmShell>();
        cut.Find("[data-testid=strip-toggle]").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid=strip-cell]")[11].Click(new MouseEventArgs { ClientX = 4000, ClientY = 90 });
            (Shell.TokenCard!.X + 340).Should().BeLessThanOrEqualTo(1000);
        }, Timeout);
    }

    [Fact]
    public async Task Collapsed_strip_renders_nothing_without_combat()
    {
        var cut = await RenderCombatAsync(start: false);

        cut.FindAll("header.ds-frame__strip .combat-strip").Should().BeEmpty();
    }

    [Fact]
    public async Task Tapping_row_selects_its_token()
    {
        var cut = await RenderCombatAsync();

        cut.FindAll("[data-testid=strip-select]")[2].Click();

        Shell.SelectedCombatTokenId.Should().Be("c");
    }

    [Fact]
    public async Task Row_menu_passes_the_turn_here()
    {
        var cut = await RenderCombatAsync();

        cut.FindAll("[data-testid=strip-menu]")[2].Click();
        cut.Find("[data-testid=menu-set-current]").Click();

        Combat.State!.CurrentEntryId.Should().Be(EntryOf("c"));
    }

    [Fact]
    public async Task Row_menu_removes_from_combat()
    {
        var cut = await RenderCombatAsync();

        cut.FindAll("[data-testid=strip-menu]")[1].Click();
        cut.Find("[data-testid=menu-remove]").Click();

        Names(cut).Should().Equal("Ana", "Cora");
    }

    [Fact]
    public async Task Row_menu_move_down_swaps_with_the_next_row()
    {
        var cut = await RenderCombatAsync();

        cut.FindAll("[data-testid=strip-menu]")[0].Click();
        cut.Find("[data-testid=menu-down]").Click();

        Names(cut).Should().Equal("Bruno", "Ana", "Cora");
    }

    [Fact]
    public async Task Header_menu_adds_a_map_token_with_its_number()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-add]").Click();
        cut.Find("[data-testid=add-initiative]").Input("12");
        cut.Find("[data-testid=add-token-d]").Click();

        Names(cut).Should().Equal("Ana", "Dani", "Bruno", "Cora");
    }

    [Fact]
    public async Task Add_list_only_offers_tokens_not_in_combat()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-add]").Click();

        cut.FindAll("[data-testid^=add-token-]").Select(b => b.GetAttribute("data-testid")).Should().Equal("add-token-d");
    }

    [Fact]
    public async Task End_combat_asks_for_confirmation_and_clears_the_strip()
    {
        var cut = await RenderCombatAsync();

        cut.Find("[data-testid=strip-end]").Click();
        var stillActive = Combat.IsActive;
        cut.FindAll(".ds-dialog-backdrop:not([hidden]) .ds-button--damage").Single().Click();

        (stillActive, Combat.IsActive, cut.FindAll("[data-testid=combat-strip]")).Should().Be((true, false, Array.Empty<IElement>()));
    }

    [Fact]
    public async Task Entry_moved_event_reorders_entries()
    {
        var cut = await RenderCombatAsync();

        Interop.OnEntryMoved(EntryOf("c"), 0);

        cut.WaitForAssertion(() => Names(cut).Should().Equal("Cora", "Ana", "Bruno"));
    }

    [Fact]
    public async Task Controls_are_disabled_in_read_only()
    {
        var withCombat = MapStateSerializer.Write(Tokens3(GenericToken("a", "Ana", hp: 20)) with
        {
            Combat = new CombatState(1, "e1", [new CombatEntry("e1", "a", 15), new CombatEntry("e2", "b", 10)]),
        });
        await OpenMapAsync(rawState: Regex.Replace(withCombat, "\"version\"\\s*:\\s*\\d+", "\"version\":999"));
        var cut = Render<DmShell>();

        new[] { "next-turn", "strip-add", "strip-end" }.Select(id => cut.Find($"[data-testid={id}]").HasAttribute("disabled"))
            .Should().AllBeEquivalentTo(true);
    }
}
