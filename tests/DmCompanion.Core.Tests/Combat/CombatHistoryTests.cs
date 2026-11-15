using DmCompanion.Core.Combat;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

public class CombatHistoryTests
{
    private static readonly ICombatRules Rules = Dnd5eCombatRules.Instance;

    private static Func<string> Ids(string prefix = "e")
    {
        var n = 0;
        return () => $"{prefix}{++n}";
    }

    private static MapToken Tok(string id) =>
        new(id, id, null, TokenSide.Enemy, 1, 10, 20, 10, 20);

    private static MapState Map() => MapState.Empty with { Tokens = [Tok("t1"), Tok("t2"), Tok("t3")] };

    // Entradas del combate: e1=t1(18), e2=t2(15), e3=t3(9). Los ids del historial salen de otro generador (l1…).
    private static MapState Started() =>
        CombatHistory.Start(Map(), [new("t1", 18), new("t2", 15), new("t3", 9)], Ids()).State;

    private static MapState Do(MapState s, Func<MapState, MapState> op, string text, Func<string> ids) =>
        CombatHistory.Record(s, op(s), text, ids);

    private static MapState Act(MapState s, string text, Func<string> ids) =>
        Do(s, x => x with { Combat = x.Combat! with { Round = x.Combat.Round + 1 } }, text, ids);

    private static string Json(MapState s) => MapStateSerializer.Write(s with { Combat = s.Combat is { } c ? c with { Log = null } : null });

    private static IReadOnlyList<CombatLogEntry> Log(MapState s) => s.Combat!.Log!;

    [Fact]
    public void Record_appends_entry_with_before_snapshot()
    {
        var ids = Ids("l");
        var s = Started();
        var next = Act(s, "Ronda", ids);
        Log(next)[^1].Before.Should().BeEquivalentTo(CombatHistory.Capture(s));
    }

    [Fact]
    public void Record_without_combat_does_nothing()
    {
        var s = Map();
        CombatHistory.Record(s, s with { Tokens = [] }, "x", Ids()).Combat.Should().BeNull();
    }

    [Fact]
    public void Record_with_the_same_object_does_nothing()
    {
        var s = Started();
        CombatHistory.Record(s, s, "x", Ids()).Should().BeSameAs(s);
    }

    [Fact]
    public void Record_drops_undone_entries()
    {
        var ids = Ids("l");
        var s = Act(Act(Started(), "a", ids), "b", ids);
        var undone = CombatHistory.Undo(s);
        Log(Act(undone, "c", ids)).Select(e => e.Text).Should().Equal(CombatHistory.StartCombatText, "a", "c");
    }

    [Fact]
    public void Record_keeps_only_the_last_100()
    {
        var ids = Ids("l");
        var s = Started();
        for (var i = 0; i < 105; i++) s = Act(s, $"a{i}", ids);
        Log(s).Select(e => e.Text).Should().Equal(Enumerable.Range(5, 100).Select(i => $"a{i}"));
    }

    private static (MapState Before, MapState Damaged) DamagedWithTempHp()
    {
        var ids = Ids("l");
        var s = Started();
        s = HitPointOps.SetTempHp(s, "t2", 5);
        s = ConditionOps.Add(s, "t2", new ConditionDraft("poisoned", "Envenenado", null, null, null), ids);
        return (s, Do(s, x => HitPointOps.Damage(x, "t2", 8, 20, Rules).State, "Daño", ids));
    }

    [Fact]
    public void Damage_in_the_review_scenario_spends_the_temp_hp()
    {
        var (_, damaged) = DamagedWithTempHp();
        damaged.Tokens.Single(t => t.Id == "t2").HpTemp.Should().BeNull();
    }

    [Fact]
    public void Undo_restores_exactly_the_state_before_damage_with_temp_hp()
    {
        var (before, damaged) = DamagedWithTempHp();
        Json(CombatHistory.Undo(damaged)).Should().Be(Json(before));
    }

    private static (MapState Before, MapState Next) NextTurnWithExpiry()
    {
        var ids = Ids("l");
        var s = Started();
        s = TurnOps.SetCurrent(s, "e3");
        s = ConditionOps.Add(s, "t1", new ConditionDraft("frightened", "Asustado", null, 1, "e1"), ids);
        return (s, Do(s, x => TurnOps.NextTurn(x).State, "Siguiente", ids));
    }

    [Fact]
    public void NextTurn_in_the_review_scenario_expires_the_condition()
    {
        var (_, next) = NextTurnWithExpiry();
        next.Tokens.Single(t => t.Id == "t1").ConditionList.Single().Expired.Should().BeTrue();
    }

    [Fact]
    public void NextTurn_in_the_review_scenario_changes_the_round()
    {
        var (before, next) = NextTurnWithExpiry();
        next.Combat!.Round.Should().Be(before.Combat!.Round + 1);
    }

    [Fact]
    public void Undo_restores_exactly_the_state_before_next_turn_with_expiry_and_round_change()
    {
        var (before, next) = NextTurnWithExpiry();
        Json(CombatHistory.Undo(next)).Should().Be(Json(before));
    }

    [Fact]
    public void Undo_twice_goes_two_steps_back()
    {
        var ids = Ids("l");
        var s0 = Started();
        var s1 = Act(s0, "a", ids);
        var s2 = Act(s1, "b", ids);
        Json(CombatHistory.Undo(CombatHistory.Undo(s2))).Should().Be(Json(s0));
    }

    [Fact]
    public void Undo_marks_the_entry_as_undone()
    {
        var ids = Ids("l");
        var s = Act(Started(), "a", ids);
        Log(CombatHistory.Undo(s))[^1].Undone.Should().BeTrue();
    }

    [Fact]
    public void Redo_reapplies_the_undone_action()
    {
        var ids = Ids("l");
        var s = Act(Act(Started(), "a", ids), "b", ids);
        var redone = CombatHistory.Redo(CombatHistory.Undo(s));
        Json(redone).Should().Be(Json(s));
    }

    [Fact]
    public void Redo_marks_the_entry_as_not_undone()
    {
        var ids = Ids("l");
        var s = Act(Started(), "a", ids);
        Log(CombatHistory.Redo(CombatHistory.Undo(s)))[^1].Undone.Should().BeFalse();
    }

    [Fact]
    public void Redo_after_two_undos_reapplies_the_oldest_first()
    {
        var ids = Ids("l");
        var s = Act(Act(Started(), "a", ids), "b", ids);
        var oneBack = CombatHistory.Undo(s);
        var redone = CombatHistory.Redo(CombatHistory.Undo(oneBack));
        Json(redone).Should().Be(Json(oneBack));
    }

    [Fact]
    public void CanRedo_is_false_without_undone_entries()
    {
        var ids = Ids("l");
        CombatHistory.CanRedo(Act(Started(), "a", ids)).Should().BeFalse();
    }

    [Fact]
    public void Undo_stops_at_start_combat()
    {
        var s = Started();
        CombatHistory.CanUndo(s).Should().BeFalse();
    }

    [Fact]
    public void Undo_at_start_combat_returns_the_same_object()
    {
        var s = Started();
        CombatHistory.Undo(s).Should().BeSameAs(s);
    }

    [Fact]
    public void Undo_after_removing_an_affected_token_cleans_its_entries()
    {
        var ids = Ids("l");
        var s = Started();
        var acted = Do(s, x => TurnOps.RemoveEntry(x, "e2"), "Sacar", ids);
        var removed = acted with { Tokens = acted.Tokens.Where(t => t.Id != "t2").ToList() };
        var undone = CombatHistory.Undo(removed);
        undone.Combat!.Entries.Select(e => e.TokenId).Should().Equal("t1", "t3");
    }

    [Fact]
    public void Undo_after_removing_a_token_keeps_the_log()
    {
        var ids = Ids("l");
        var s = Started();
        var acted = Do(s, x => TurnOps.RemoveEntry(x, "e2"), "Sacar", ids);
        var removed = acted with { Tokens = acted.Tokens.Where(t => t.Id != "t2").ToList() };
        Log(CombatHistory.Undo(removed)).Select(e => e.Text).Should().Equal(CombatHistory.StartCombatText, "Sacar");
    }

    [Fact]
    public void Undo_never_touches_position_of_tokens()
    {
        var ids = Ids("l");
        var s = Started();
        var damaged = Do(s, x => HitPointOps.Damage(x, "t1", 3, 20, Rules).State, "Daño", ids);
        var moved = damaged with { Tokens = damaged.Tokens.Select(t => t with { X = 99 }).ToList() };
        CombatHistory.Undo(moved).Tokens.Select(t => t.X).Should().OnlyContain(x => x == 99);
    }

    [Fact]
    public void End_keeps_last_combat_and_Recover_restores_it_exactly()
    {
        var ids = Ids("l");
        var s = ConditionOps.Add(Started(), "t1", new ConditionDraft("poisoned", "Envenenado", null, 3, "e1"), ids);
        s = Act(s, "a", ids);
        var ended = CombatHistory.End(s);
        var recovered = CombatHistory.Recover(ended, ids);
        Json(recovered).Should().Be(Json(s));
    }

    [Fact]
    public void End_moves_the_combat_to_last_combat()
    {
        var ended = CombatHistory.End(Act(Started(), "a", Ids("l")));
        ended.Combat.Should().BeNull();
        ended.LastCombat!.Combat!.Log.Should().HaveCount(2);
    }

    [Fact]
    public void Recover_clears_last_combat()
    {
        var ids = Ids("l");
        CombatHistory.Recover(CombatHistory.End(Started()), ids).LastCombat.Should().BeNull();
    }

    [Fact]
    public void Recover_appends_a_non_undoable_entry_and_keeps_the_old_log()
    {
        var ids = Ids("l");
        var s = Act(Started(), "a", ids);
        var recovered = CombatHistory.Recover(CombatHistory.End(s), ids);
        Log(recovered).Select(e => e.Text).Should().Equal(CombatHistory.StartCombatText, "a", CombatHistory.RecoveredCombatText);
    }

    [Fact]
    public void Start_discards_last_combat()
    {
        var ids = Ids("l");
        var ended = CombatHistory.End(Started());
        var again = CombatHistory.Start(ended, [new("t1", 5)], ids).State;
        again.LastCombat.Should().BeNull();
    }

    [Fact]
    public void Recover_without_last_combat_does_nothing()
    {
        var s = Map();
        CombatHistory.Recover(s, Ids()).Should().BeSameAs(s);
    }

    [Fact]
    public void Recover_with_active_combat_does_nothing()
    {
        var ids = Ids("l");
        var s = Started() with { LastCombat = new CombatSnapshot(null, []) };
        CombatHistory.Recover(s, ids).Should().BeSameAs(s);
    }

    [Fact]
    public void Recover_ignores_tokens_that_no_longer_exist()
    {
        var ids = Ids("l");
        var ended = CombatHistory.End(Started());
        var gone = ended with { Tokens = ended.Tokens.Where(t => t.Id != "t3").ToList() };
        CombatHistory.Recover(gone, ids).Combat!.Entries.Select(e => e.TokenId).Should().Equal("t1", "t2");
    }

    private static MapState RecoveredAfterAction(Func<string> ids) =>
        CombatHistory.Recover(CombatHistory.End(Act(Started(), "a", ids)), ids);

    [Fact]
    public void Undo_after_Recover_undoes_the_last_action_before_ending()
    {
        var ids = Ids("l");
        var undone = CombatHistory.Undo(RecoveredAfterAction(ids));
        undone.Combat!.Round.Should().Be(1);
    }

    [Fact]
    public void CanUndo_after_Recover_is_true_but_not_when_only_the_start_remains()
    {
        var ids = Ids("l");
        var onlyStart = CombatHistory.Recover(CombatHistory.End(Started()), ids);
        (CombatHistory.CanUndo(RecoveredAfterAction(ids)), CombatHistory.CanUndo(onlyStart)).Should().Be((true, false));
    }

    [Fact]
    public void Redo_after_Recover_and_Undo_reapplies_the_action()
    {
        var ids = Ids("l");
        var redone = CombatHistory.Redo(CombatHistory.Undo(RecoveredAfterAction(ids)));
        (redone.Combat!.Round, CombatHistory.CanRedo(redone)).Should().Be((2, false));
    }

    [Fact]
    public void Redo_of_an_action_done_after_Recover_restores_it_exactly()
    {
        var ids = Ids("l");
        var withB = Act(RecoveredAfterAction(ids), "b", ids);
        var undone = CombatHistory.Undo(withB);
        var canRedo = CombatHistory.CanRedo(undone);
        (canRedo, Json(CombatHistory.Redo(undone))).Should().Be((true, Json(withB)));
    }

    [Fact]
    public void Undoing_two_actions_after_Recover_and_redoing_twice_returns_to_the_final_state()
    {
        var ids = Ids("l");
        var final = Act(Act(RecoveredAfterAction(ids), "a2", ids), "b2", ids);
        var back = CombatHistory.Undo(CombatHistory.Undo(final));
        var again = CombatHistory.Redo(CombatHistory.Redo(back));
        (Json(again), CombatHistory.CanRedo(again)).Should().Be((Json(final), false));
    }
}
