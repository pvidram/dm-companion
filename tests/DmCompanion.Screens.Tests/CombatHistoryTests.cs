using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;

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
    [InlineData("[data-testid=history-popover] button[aria-label=Cerrar]")]
    [InlineData("[data-testid=history-backdrop]")]
    public async Task Popover_closes_with_its_close_button_and_by_tapping_outside(string selector)
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        await cut.InvokeAsync(() => cut.Find(selector).Click());

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

        await cut.InvokeAsync(() => Windows.OpenTokenCard("a", 100, 100));

        cut.FindAll("[data-testid=history-popover]").Should().BeEmpty();
    }

    [Fact]
    public async Task Popover_comes_before_the_token_card_in_the_dom_so_it_never_covers_it()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => Windows.OpenTokenCard("a", 100, 100));
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());

        var order = cut.FindAll(".ds-quick-card").Select(e => e.GetAttribute("data-testid") == "history-popover").ToArray();

        order.Should().Equal(true, false);
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

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "enter");
    }

    private PanelLayoutStore Layout => Services.GetRequiredService<PanelLayoutStore>();
    private FakeWindowFrameInterop Frames => Services.GetRequiredService<FakeWindowFrameInterop>();

    private async Task<IRenderedComponent<DmShell>> OpenCollapsedHistoryAsync()
    {
        var cut = await RenderAsync();
        await Collapse(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=history-toggle]").Click());
        return cut;
    }

    private static Task TogglePinAsync(IRenderedComponent<DmShell> cut) => cut.InvokeAsync(() => cut.Find("[data-testid=history-popover] button[aria-label=Anclar]").Click());

    [Fact]
    public async Task Pinned_history_has_no_backdrop()
    {
        var cut = await OpenCollapsedHistoryAsync();

        await TogglePinAsync(cut);

        (Layout.IsPinned(LayoutKeys.History), cut.FindAll("[data-testid=history-backdrop]").Count).Should().Be((true, 0));
    }

    [Fact]
    public async Task Pinned_history_reopens_at_its_anchor()
    {
        var anchor = new WindowAnchor(WindowCorner.BottomRight, 30, 40);
        Layout.SetPinned(LayoutKeys.History, true);
        Layout.SetAnchor(LayoutKeys.History, anchor);

        await OpenCollapsedHistoryAsync();

        Frames.Attached.Last().Anchor.Should().Be(anchor);
    }

    [Fact]
    public async Task Unpinning_forgets_the_anchor()
    {
        Layout.SetPinned(LayoutKeys.History, true);
        Layout.SetAnchor(LayoutKeys.History, new WindowAnchor(WindowCorner.TopLeft, 5, 6));
        var cut = await OpenCollapsedHistoryAsync();

        await cut.InvokeAsync(() => cut.Find("[data-testid=history-popover] button[aria-label=Desanclar]").Click());

        (Layout.IsPinned(LayoutKeys.History), Layout.GetAnchor(LayoutKeys.History)).Should().Be((false, null));
    }

    [Fact]
    public async Task Closing_the_history_also_unpins_it()
    {
        Layout.SetPinned(LayoutKeys.History, true);
        var cut = await OpenCollapsedHistoryAsync();

        await cut.InvokeAsync(() => cut.Find("[data-testid=history-popover] button[aria-label=Cerrar]").Click());

        (Layout.IsPinned(LayoutKeys.History), Shell.HistoryOpen).Should().Be((false, false));
    }

    [Fact]
    public async Task Moving_the_pinned_history_stores_its_anchor_and_size()
    {
        Layout.SetPinned(LayoutKeys.History, true);
        var cut = await OpenCollapsedHistoryAsync();
        var anchor = new WindowAnchor(WindowCorner.TopRight, 12, 20);

        await cut.InvokeAsync(() => Frames.Listener!.OnFrameChangedAsync(new WindowFrameChange(1, 2, new WindowSize(300, 200), anchor)));

        (Layout.GetAnchor(LayoutKeys.History), Layout.GetSize(LayoutKeys.History)).Should().Be((anchor, new WindowSize(300, 200)));
    }

    [Fact]
    public async Task History_header_keeps_undo_and_redo()
    {
        var cut = await OpenCollapsedHistoryAsync();

        cut.FindAll("[data-testid=history-popover] header [data-testid=history-undo], [data-testid=history-popover] header [data-testid=history-redo]")
            .Should().HaveCount(2);
    }

    [Fact]
    public async Task History_without_a_stored_size_carries_the_unsized_cap_class()
    {
        var cut = await OpenCollapsedHistoryAsync();

        cut.Find("[data-testid=history-popover]").ClassList.Should().Contain("combat-history-popover--unsized");
    }

    [Fact]
    public async Task History_with_a_stored_size_has_no_cap_class()
    {
        Layout.SetSize(LayoutKeys.History, new WindowSize(500, 600));

        var cut = await OpenCollapsedHistoryAsync();

        cut.Find("[data-testid=history-popover]").ClassList.Should().NotContain("combat-history-popover--unsized");
    }
}
