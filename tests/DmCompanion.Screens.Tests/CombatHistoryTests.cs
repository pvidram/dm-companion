using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Screens.Components;

namespace DmCompanion.Screens.Tests;

/// <summary>Historial, deshacer/rehacer y recuperación del combate (spec §6.2–§6.4).</summary>
public sealed class CombatHistoryTests : CombatUiTestBase
{
    private static string[] Entries(IRenderedComponent<DmShell> cut) =>
        [.. cut.FindAll("[data-testid=history-entry]").Select(e => e.TextContent.Trim())];

    private static Task OpenHistory(IRenderedComponent<DmShell> cut) => cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

    [Fact]
    public async Task History_is_folded_by_default()
    {
        var cut = await RenderAsync();

        cut.FindAll("[data-testid=history-entry]").Should().BeEmpty();
    }

    [Fact]
    public async Task History_shows_newest_first()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.NextTurn());
        await OpenHistory(cut);

        Entries(cut).Should().Equal(Combat.Log.Select(e => e.Text).Reverse());
    }

    [Fact]
    public async Task History_shows_undone_entries_struck_through()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.Undo());
        await OpenHistory(cut);

        cut.Find("[data-testid=history-entry]").ClassList.Should().Contain("combat-history__entry--undone");
    }

    [Fact]
    public async Task History_does_not_strike_live_entries()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await OpenHistory(cut);

        cut.Find("[data-testid=history-entry]").ClassList.Should().NotContain("combat-history__entry--undone");
    }

    [Fact]
    public async Task Undo_and_redo_buttons_follow_can_undo()
    {
        var cut = await RenderAsync();
        var initial = (cut.Find("[data-testid=undo]").HasAttribute("disabled"), cut.Find("[data-testid=redo]").HasAttribute("disabled"));
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        cut.Render();
        var afterAction = (cut.Find("[data-testid=undo]").HasAttribute("disabled"), cut.Find("[data-testid=redo]").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=undo]").Click());
        var afterUndo = (cut.Find("[data-testid=undo]").HasAttribute("disabled"), cut.Find("[data-testid=redo]").HasAttribute("disabled"));

        new[] { initial, afterAction, afterUndo }.Should().Equal((true, true), (false, true), (true, false));
    }

    [Fact]
    public async Task Redo_button_reapplies()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.Undo());
        cut.Render();

        await cut.InvokeAsync(() => cut.Find("[data-testid=redo]").Click());

        Ana.HpCurrent.Should().Be(17);
    }

    [Fact]
    public async Task Undo_and_redo_are_disabled_in_read_only()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.Undo());
        MakeReadOnly();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());

        (cut.Find("[data-testid=undo]").HasAttribute("disabled"), cut.Find("[data-testid=redo]").HasAttribute("disabled")).Should().Be((true, true));
    }

    [Fact]
    public async Task Collapsed_strip_has_undo()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());

        await cut.InvokeAsync(() => cut.Find("[data-testid=undo]").Click());

        Ana.HpCurrent.Should().BeNull();
    }

    [Fact]
    public async Task Combat_ended_notice_offers_undo()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.End());
        cut.Render();
        var text = cut.Find("[data-testid=notice-ended]").TextContent;

        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-ended] button").First(b => b.TextContent.Trim() == "Deshacer").Click());

        (text.StartsWith("Combate terminado"), Combat.IsActive).Should().Be((true, true));
    }

    [Fact]
    public async Task Combat_ended_notice_survives_the_first_render()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.End());
        cut.Render();
        cut.Render();

        cut.FindAll("[data-testid=notice-ended]").Should().HaveCount(1);
    }

    [Fact]
    public async Task Undone_entries_have_an_accessible_text()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.Undo());
        await OpenHistory(cut);

        cut.Find("[data-testid=history-entry]").TextContent.Should().Contain("(deshecho)");
    }

    [Fact]
    public async Task Recovering_from_the_notice_restores_the_log_and_clears_the_notice()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.End());
        cut.Render();

        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-ended] button").First(b => b.TextContent.Trim() == "Deshacer").Click());

        (Combat.Log.Any(e => e.Text == "Combate recuperado"), Combat.Log.Count > 2, Combat.Notices.OfType<CombatEnded>().Count(), cut.FindAll("[data-testid=notice-ended]").Count)
            .Should().Be((true, true, 0, 0));
    }

    [Fact]
    public async Task Recovering_from_the_dialog_restores_the_log_and_clears_the_notice()
    {
        await RenderAsync();
        Combat.Damage("a", 3);
        Combat.End();
        Shell.OpenStartCombat();
        var dialog = Render<StartCombatDialog>();

        await dialog.InvokeAsync(() => dialog.Find("[data-testid=recover-combat]").Click());

        (Combat.Log.Any(e => e.Text == "Combate recuperado"), Combat.Log.Count > 2, Combat.Notices.OfType<CombatEnded>().Count())
            .Should().Be((true, true, 0));
    }

    [Fact]
    public async Task Start_dialog_offers_recover_previous_combat()
    {
        await RenderAsync();
        Combat.End();
        Shell.OpenStartCombat();
        var dialog = Render<StartCombatDialog>();

        await dialog.InvokeAsync(() => dialog.FindAll("button").First(b => b.TextContent.Trim() == "Recuperar el combate anterior").Click());

        Combat.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Start_dialog_hides_recover_without_a_previous_combat()
    {
        await RenderAsync(start: false);
        Shell.OpenStartCombat();
        var dialog = Render<StartCombatDialog>();

        dialog.FindAll("[data-testid=recover-combat]").Should().BeEmpty();
    }

    private static Task Collapse(IRenderedComponent<DmShell> cut) => cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());

    [Fact]
    public async Task Expanded_strip_pins_the_history_toggle_under_the_header_with_its_count()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        cut.Render();

        var toggle = cut.Find(".combat-strip__sticky [data-testid=history-toggle]");

        toggle.TextContent.Trim().Should().Be($"Historial ({Combat.Log.Count})");
    }

    [Fact]
    public async Task Collapsed_strip_has_a_history_button()
    {
        var cut = await RenderAsync();
        await Collapse(cut);

        cut.FindAll("[data-testid=history-toggle]").Should().HaveCount(1);
    }

    [Fact]
    public async Task Collapsed_history_button_opens_the_popover_with_the_entries()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await Collapse(cut);

        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        Entries(cut).Should().Equal(Combat.Log.Select(e => e.Text).Reverse());
    }

    [Fact]
    public async Task Collapsed_history_is_closed_until_the_button_is_pressed()
    {
        var cut = await RenderAsync();
        await Collapse(cut);

        cut.FindAll("[data-testid=history-popover]").Should().BeEmpty();
    }

    [Theory]
    [InlineData("history-close")]
    [InlineData("history-backdrop")]
    public async Task Popover_closes_with_its_close_button_and_by_tapping_outside(string testId)
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        await cut.InvokeAsync(() => cut.Find($"[data-testid={testId}]").Click());

        cut.FindAll("[data-testid=history-popover]").Should().BeEmpty();
    }

    [Fact]
    public async Task Popover_undo_and_redo_work_inside_it()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        await cut.InvokeAsync(() => cut.Find("[data-testid=history-undo]").Click());
        var undone = Ana.HpCurrent;
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-redo]").Click());

        (undone, Ana.HpCurrent).Should().Be((null, 17));
    }

    [Fact]
    public async Task Popover_undo_and_redo_are_disabled_in_read_only()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.Damage("a", 3));
        await cut.InvokeAsync(() => Combat.Undo());
        MakeReadOnly();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        (cut.Find("[data-testid=history-undo]").HasAttribute("disabled"), cut.Find("[data-testid=history-redo]").HasAttribute("disabled")).Should().Be((true, true));
    }

    [Fact]
    public async Task Expanding_the_strip_closes_the_popover()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());
        await Collapse(cut);

        cut.FindAll("[data-testid=history-popover]").Should().BeEmpty();
    }

    [Fact]
    public async Task Opening_a_token_card_closes_the_popover()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        await cut.InvokeAsync(() => Shell.OpenTokenCard(new Screens.Services.TokenCardRequest("a", 100, 100)));

        cut.FindAll("[data-testid=history-popover]").Should().BeEmpty();
    }

    [Fact]
    public async Task Popover_comes_before_the_token_card_in_the_dom_so_it_never_covers_it()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        var order = cut.FindAll("[data-testid=history-popover], .ds-quick-card").Select(e => e.ClassList.Contains("ds-quick-card")).ToArray();

        order.Should().Equal(false, true);
    }

    [Fact]
    public async Task Ending_the_combat_closes_the_popover_for_good()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());
        await cut.InvokeAsync(() => Combat.End());

        Shell.HistoryOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Shell_reset_closes_the_popover()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());
        await cut.InvokeAsync(() => Shell.Reset());

        Shell.HistoryOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Popover_takes_the_focus_when_it_opens()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "Blazor._internal.domWrapper.focus");
    }
}
