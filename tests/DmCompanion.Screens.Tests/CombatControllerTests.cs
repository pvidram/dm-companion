using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

public sealed class CombatControllerTests : TokenTestBase
{
    private CombatController Combat => Services.GetRequiredService<CombatController>();

    private Task OpenWithTokensAsync() => OpenMapAsync(MapState.Empty with
    {
        Tokens = [GenericToken("a", "Ana", hp: 20), GenericToken("b", "Bruno", hp: 20)],
    });

    private static InitiativeEntry[] Both() => [new("a", 15), new("b", 10)];

    private string EntryOf(string tokenId) => Combat.State!.Entries.First(e => e.TokenId == tokenId).Id;

    [Fact]
    public async Task Start_saves_combat_in_the_state_file()
    {
        await OpenWithTokensAsync();

        Combat.Start(Both());
        await Session.FlushAsync();

        File.ReadAllText(PathOf(Session.Current!.StatePath)).Should().Contain("\"combat\"");
    }

    [Fact]
    public async Task Next_turn_with_expiring_condition_adds_a_notice()
    {
        await OpenWithTokensAsync();
        Combat.Start(Both());
        Combat.AddCondition("a", new ConditionDraft(null, "Bendición", null, 1, EntryOf("a")));

        Combat.NextTurn();
        Combat.NextTurn();

        Combat.Notices.Should().ContainSingle().Which.Should().BeOfType<ConditionExpired>();
    }

    [Fact]
    public async Task Damage_to_concentrating_token_adds_a_concentration_notice()
    {
        await OpenWithTokensAsync();
        Combat.Start(Both());
        Combat.AddCondition("a", new ConditionDraft("concentration", "Concentración", null, null, null));

        Combat.Damage("a", 5);

        Combat.Notices.Should().ContainSingle().Which.Should().BeOfType<ConcentrationCheck>();
    }

    [Fact]
    public async Task Operations_do_nothing_when_read_only()
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [GenericToken("a", "Ana", hp: 20)] },
            rawState: "{\"version\":999,\"tokens\":[]}");
        Session.IsReadOnly.Should().BeTrue();

        Combat.Start([new InitiativeEntry("a", 10)]);

        Combat.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Notices_are_cleared_when_another_map_opens()
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [GenericToken("a", "Ana", hp: 20)] },
            extraNotes: () => WriteNote("Mapas/otro.md", "---\ntype: map\nname: Otro\n---\n"));
        Combat.Start([new InitiativeEntry("a", 10)]);
        Combat.AddCondition("a", new ConditionDraft("concentration", "Concentración", null, null, null));
        Combat.Damage("a", 5);

        await Session.OpenAsync("Mapas/otro.md");

        Combat.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Pending_notices_are_dropped_when_combat_ends()
    {
        await OpenWithTokensAsync();
        Combat.Start(Both());
        Combat.AddCondition("a", new ConditionDraft("concentration", "Concentración", null, null, null));
        Combat.Damage("a", 5);

        Combat.End();

        Combat.Notices.Should().NotContain(n => n is ConcentrationCheck);
    }

    [Fact]
    public async Task Notices_are_cleared_when_a_new_combat_starts()
    {
        await OpenWithTokensAsync();
        Combat.Start(Both());
        Combat.AddCondition("a", new ConditionDraft("concentration", "Concentración", null, null, null));
        Combat.Damage("a", 5);

        Combat.Start(Both());

        Combat.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Strip_size_is_remembered_in_preferences()
    {
        await OpenWithTokensAsync();
        Combat.StripExpanded = false;

        new CombatController(Session, Services.GetRequiredService<TokenHitPoints>(), Services.GetRequiredService<IAppPreferences>(), TimeProvider.System)
            .StripExpanded.Should().BeFalse();
    }

    [Fact]
    public async Task Start_expands_the_strip()
    {
        await OpenWithTokensAsync();
        Combat.StripExpanded = false;

        Combat.Start(Both());

        Combat.StripExpanded.Should().BeTrue();
    }

    private Task OpenForLogAsync() => OpenMapAsync(MapState.Empty with
    {
        Tokens = [GenericToken("a", "Lyra", hp: 11, side: TokenSide.Pc), GenericToken("g", "Goblin 1", hp: 11), GenericToken("f", "Aldric", hp: 11, side: TokenSide.Ally)],
    });

    private void StartLog()
    {
        Combat.Start([new InitiativeEntry("a", 15), new InitiativeEntry("g", 10)]);
    }

    [Theory]
    [InlineData("damage", "Goblin 1: −7 HP (4/11)")]
    [InlineData("heal", "Goblin 1: +3 HP (7/11)")]
    [InlineData("hp", "Goblin 1: −9 HP (2/11)")]
    [InlineData("temp", "Goblin 1: 5 HP temporales")]
    [InlineData("condition", "Lyra: Envenenado (3 rondas)")]
    [InlineData("next", "R1 · Turno de Goblin 1")]
    [InlineData("dead", "Goblin 1: muerto")]
    [InlineData("revive", "Goblin 1: revive")]
    [InlineData("saves", "Goblin 1: salvaciones de muerte (1 éxito, 2 fallos)")]
    public async Task Each_operation_adds_a_log_entry_with_its_text(string operation, string expected)
    {
        await OpenForLogAsync();
        StartLog();
        if (operation == "heal") Combat.SetHp("g", 4);
        if (operation == "revive") Combat.MarkDead("g");
        var before = Combat.Log.Count;

        switch (operation)
        {
            case "damage": Combat.Damage("g", 7); break;
            case "heal": Combat.Heal("g", 3); break;
            case "hp": Combat.SetHp("g", 2); break;
            case "temp": Combat.SetTempHp("g", 5); break;
            case "condition": Combat.AddCondition("a", new ConditionDraft(null, "Envenenado", null, 3, null)); break;
            case "next": Combat.NextTurn(); break;
            case "dead": Combat.MarkDead("g"); break;
            case "revive": Combat.Revive("g"); break;
            case "saves": Combat.SetDeathSaves("g", 1, 2); break;
        }

        (Combat.Log.Count - before, Combat.Log[^1].Text).Should().Be((1, expected));
    }

    [Fact]
    public async Task Start_adds_the_start_combat_entry()
    {
        await OpenForLogAsync();

        StartLog();

        Combat.Log.Should().ContainSingle().Which.Text.Should().Be(CombatHistory.StartCombatText);
    }

    [Fact]
    public async Task Undo_and_redo_through_the_controller()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.Damage("g", 7);

        Combat.Undo();
        var afterUndo = Tokens.First(t => t.Id == "g").HpCurrent;
        Combat.Redo();

        (afterUndo, Tokens.First(t => t.Id == "g").HpCurrent).Should().Be(((int?)null, (int?)4));
    }

    [Fact]
    public async Task Undo_and_redo_report_availability()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.Damage("g", 7);
        Combat.Undo();

        (Combat.CanUndo, Combat.CanRedo).Should().Be((false, true));
    }

    [Fact]
    public async Task Undo_clears_pending_notices()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.AddCondition("g", new ConditionDraft("concentration", "Concentración", null, null, null));
        Combat.Damage("g", 5);
        Combat.Notices.Should().NotBeEmpty();

        Combat.Undo();

        Combat.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task End_shows_combat_ended_notice_that_expires_after_10_seconds()
    {
        await OpenForLogAsync();
        var time = new FakeTimeProvider();
        var combat = new CombatController(Session, Services.GetRequiredService<TokenHitPoints>(), Services.GetRequiredService<IAppPreferences>(), time);
        combat.Start([new InitiativeEntry("a", 15)]);

        combat.End();
        var shown = combat.Notices.OfType<CombatEnded>().Count();
        time.Advance(TimeSpan.FromSeconds(10));

        (shown, combat.Notices.Count).Should().Be((1, 0));
    }

    [Fact]
    public async Task Combat_ended_notice_disappears_with_the_next_action()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.End();

        Combat.Start([new InitiativeEntry("a", 15)]);

        Combat.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Recover_restores_the_ended_combat()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.Damage("g", 7);
        Combat.End();
        Combat.CanRecover.Should().BeTrue();

        Combat.Recover();

        (Combat.IsActive, Tokens.First(t => t.Id == "g").HpCurrent, Combat.Log[^1].Text, Combat.Notices.Count).Should()
            .Be((true, 4, CombatHistory.RecoveredCombatText, 0));
    }

    [Fact]
    public async Task Undo_after_ending_and_recovering_undoes_the_last_combat_action()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.Damage("g", 7);
        Combat.End();
        Combat.Recover();
        Combat.CanUndo.Should().BeTrue();

        Combat.Undo();

        (Tokens.First(t => t.Id == "g").HpCurrent, Combat.IsActive).Should().Be(((int?)null, true));
    }

    [Fact]
    public async Task Third_failed_death_save_suggests_marking_dead()
    {
        await OpenForLogAsync();
        StartLog();

        Combat.SetDeathSaves("a", 0, 3);

        Combat.Notices.Should().ContainSingle().Which.Should().Be(new ThreeFailures("a"));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("f")]
    public async Task Third_failed_death_save_of_an_enemy_or_ally_raises_no_notice(string id)
    {
        await OpenForLogAsync();
        StartLog();

        Combat.SetDeathSaves(id, 0, 3);

        Combat.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Third_failed_death_save_does_not_mark_the_token_dead()
    {
        await OpenForLogAsync();
        StartLog();

        Combat.SetDeathSaves("a", 0, 3);

        Tokens.First(t => t.Id == "a").IsDead.Should().BeFalse();
    }

    [Fact]
    public async Task Card_hp_edit_during_combat_is_logged()
    {
        await OpenForLogAsync();
        StartLog();
        Windows.OpenTokenCard("g", 100, 100);
        var cut = Render<DmCompanion.Screens.Components.TokenCardLayer>();
        cut.Find("button[data-testid=token-hp-value]").Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input("-7");
        input.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Combat.Log[^1].Text.Should().Be("Goblin 1: −7 HP (4/11)");
    }

    [Fact]
    public async Task Operations_outside_combat_are_not_logged()
    {
        await OpenForLogAsync();

        Combat.Damage("g", 7);
        Combat.MarkDead("g");

        (Combat.Log.Count, Tokens.First(t => t.Id == "g").HpCurrent, Tokens.First(t => t.Id == "g").IsDead).Should()
            .Be((0, 4, true));
    }

    [Fact]
    public async Task An_operation_that_changes_nothing_is_not_logged()
    {
        await OpenForLogAsync();
        StartLog();

        Combat.Revive("g");

        Combat.Log.Should().ContainSingle();
    }

    [Fact]
    public async Task Removing_the_last_entry_keeps_the_combat_recoverable()
    {
        await OpenForLogAsync();
        Combat.Start([new InitiativeEntry("a", 15)]);
        Combat.Damage("a", 3);

        Combat.RemoveEntry(Combat.State!.Entries[0].Id);

        (Combat.IsActive, Combat.CanRecover, Combat.Notices.OfType<CombatEnded>().Count()).Should().Be((false, true, 1));
    }

    [Fact]
    public async Task Recovering_after_removing_the_last_entry_restores_the_history()
    {
        await OpenForLogAsync();
        Combat.Start([new InitiativeEntry("a", 15)]);
        Combat.Damage("a", 3);
        Combat.RemoveEntry(Combat.State!.Entries[0].Id);

        Combat.Recover();

        Combat.Log.Select(e => e.Text).Should().Contain("Lyra: −3 HP (8/11)");
    }

    [Fact]
    public async Task Redo_of_the_third_failed_save_does_not_raise_the_notice_again()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.SetDeathSaves("a", 0, 3);
        Combat.Undo();

        Combat.Redo();

        Combat.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Recover_does_not_raise_three_failures_notices()
    {
        await OpenForLogAsync();
        StartLog();
        Combat.SetDeathSaves("a", 0, 3);
        Combat.End();

        Combat.Recover();

        Combat.Notices.Should().BeEmpty();
    }

    [Theory]
    [InlineData("-7", 1)]
    [InlineData("+3", 11)]
    [InlineData("6", 6)]
    public async Task Hp_text_is_interpreted_on_the_current_state(string text, int expected)
    {
        await OpenForLogAsync();
        StartLog();
        Combat.SetHp("g", 8);

        Combat.SetHpFromText("g", text);

        Tokens.First(t => t.Id == "g").HpCurrent.Should().Be(expected);
    }

    [Fact]
    public async Task Invalid_hp_text_is_reported_and_changes_nothing()
    {
        await OpenForLogAsync();
        StartLog();

        var valid = Combat.SetHpFromText("g", "abc");

        (valid, Combat.Log.Count).Should().Be((false, 1));
    }
}
