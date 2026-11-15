using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class MapStateRepairTests
{
    private static MapToken Token(string id, int? hpTemp = null, IReadOnlyList<TokenCondition>? conditions = null, DeathSaves? saves = null) =>
        new(id, id, null, TokenSide.Pc, 1, 0, 0, null, null, null, hpTemp, conditions, saves);

    private static MapState State(CombatState? combat, params MapToken[] tokens) =>
        new(MapState.CurrentVersion, GridSettings.Default, tokens, [], combat);

    private static CombatEntry Entry(string id, string tokenId) => new(id, tokenId, 10);

    [Fact]
    public void Consistent_state_has_no_repairs()
    {
        var s = State(new CombatState(2, "e1", [Entry("e1", "a"), Entry("e2", "b")]), Token("a"), Token("b"));

        var (state, repairs) = MapStateRepair.Repair(s);

        (repairs, state.Combat!.Entries.Count).Should().Be((0, 2));
    }

    [Fact]
    public void Entry_for_missing_token_is_dropped()
    {
        var s = State(new CombatState(1, "e1", [Entry("e1", "a"), Entry("e2", "ghost")]), Token("a"));

        var (state, repairs) = MapStateRepair.Repair(s);

        (string.Join(',', state.Combat!.Entries.Select(e => e.Id)), repairs > 0).Should().Be(("e1", true));
    }

    [Fact]
    public void Duplicate_entry_ids_keep_the_first()
    {
        var s = State(new CombatState(1, "e1", [new CombatEntry("e1", "a", 5), new CombatEntry("e1", "b", 9)]), Token("a"), Token("b"));

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Combat!.Entries.Single().TokenId, repairs > 0).Should().Be(("a", true));
    }

    [Fact]
    public void Unknown_current_entry_moves_turn_to_first_entry()
    {
        var s = State(new CombatState(1, "nope", [Entry("e1", "a"), Entry("e2", "b")]), Token("a"), Token("b"));

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Combat!.CurrentEntryId, repairs > 0).Should().Be(("e1", true));
    }

    [Fact]
    public void Duration_anchored_to_missing_entry_is_reanchored_to_the_token_entry_or_dropped()
    {
        var withOwn = Token("a", conditions: [new TokenCondition("c1", "poisoned", "Envenenado", null, new ConditionDuration("gone", 2), false)]);
        var withoutOwn = Token("b", conditions: [new TokenCondition("c2", null, "Bendecido", null, new ConditionDuration("gone", 2), false)]);
        var s = State(new CombatState(1, "e1", [Entry("e1", "a")]), withOwn, withoutOwn);

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Tokens[0].ConditionList[0].Duration, state.Tokens[1].ConditionList[0].Duration, repairs > 0)
            .Should().Be((new ConditionDuration("e1", 2), null, true));
    }

    [Fact]
    public void Negative_values_are_clamped_to_zero()
    {
        var cond = new TokenCondition("c1", null, "X", null, new ConditionDuration("e1", -3), false);
        var s = State(new CombatState(0, "e1", [Entry("e1", "a")]), Token("a", -4, [cond], new DeathSaves(7, -1)));

        var (state, repairs) = MapStateRepair.Repair(s);

        var token = state.Tokens[0];
        (state.Combat!.Round, token.HpTemp, token.ConditionList[0].Duration!.TurnsLeft, token.DeathSaves, repairs > 0)
            .Should().Be((1, 0, 0, new DeathSaves(3, 0), true));
    }

    [Fact]
    public void Combat_with_no_entries_is_removed()
    {
        var s = State(new CombatState(3, null, []), Token("a"));

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Combat, repairs > 0).Should().Be((null, true));
    }

    [Theory]
    [InlineData(null, "X")]
    [InlineData("c1", null)]
    public void Condition_without_id_or_label_is_dropped_and_counted(string? id, string? label)
    {
        var broken = new TokenCondition(id!, null, label!, null, null, false);
        var s = State(null, Token("a", conditions: [broken]));

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Tokens[0].ConditionList.Count, repairs).Should().Be((0, 1));
    }

    [Fact]
    public void Null_condition_is_dropped()
    {
        var kept = new TokenCondition("c1", null, "Bendecido", null, null, false);
        var s = State(null, Token("a", conditions: [kept, null!]));

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Tokens[0].ConditionList.Select(c => c.Id).SingleOrDefault(), repairs).Should().Be(("c1", 1));
    }

    [Fact]
    public void Duration_without_combat_is_dropped()
    {
        var cond = new TokenCondition("c1", null, "Bendecido", null, new ConditionDuration("e1", 2), false);
        var s = State(null, Token("a", conditions: [cond]));

        var (state, repairs) = MapStateRepair.Repair(s);

        (state.Tokens[0].ConditionList[0].Duration, repairs).Should().Be((null, 1));
    }

    private static CombatLogEntry Log(string id, bool withBefore = true, bool undoable = true) =>
        new(id, 1, id, withBefore ? new CombatSnapshot(null, []) : null, null, false, undoable);

    private static CombatState WithLog(IReadOnlyList<CombatLogEntry>? log) =>
        new(1, "e1", [Entry("e1", "a")], log);

    [Fact]
    public void Log_over_100_keeps_the_newest_100()
    {
        var log = Enumerable.Range(1, 105).Select(i => Log($"l{i}")).ToList();

        var (state, repairs) = MapStateRepair.Repair(State(WithLog(log), Token("a")));

        (state.Combat!.Log!.Count, state.Combat.Log[0].Id, repairs).Should().Be((100, "l6", 5));
    }

    [Fact]
    public void Log_entry_without_before_is_dropped()
    {
        var (state, repairs) = MapStateRepair.Repair(State(WithLog([Log("l1"), Log("l2", withBefore: false)]), Token("a")));

        (string.Join(',', state.Combat!.Log!.Select(e => e.Id)), repairs).Should().Be(("l1", 1));
    }

    [Fact]
    public void Log_entry_without_before_is_kept_when_not_undoable()
    {
        var (state, repairs) = MapStateRepair.Repair(State(WithLog([Log("l1", withBefore: false, undoable: false)]), Token("a")));

        (state.Combat!.Log!.Count, repairs).Should().Be((1, 0));
    }

    [Fact]
    public void Duplicate_log_ids_keep_the_first()
    {
        var first = Log("l1") with { Text = "primero" };
        var (state, repairs) = MapStateRepair.Repair(State(WithLog([first, Log("l1")]), Token("a")));

        (state.Combat!.Log!.Single().Text, repairs).Should().Be(("primero", 1));
    }

    [Fact]
    public void Null_log_entry_is_dropped()
    {
        var (state, repairs) = MapStateRepair.Repair(State(WithLog([Log("l1"), null!]), Token("a")));

        (state.Combat!.Log!.Count, repairs).Should().Be((1, 1));
    }

    [Fact]
    public void Log_snapshot_with_null_token_list_becomes_empty()
    {
        var entry = Log("l1") with { Before = new CombatSnapshot(null, null!) };

        var (state, _) = MapStateRepair.Repair(State(WithLog([entry]), Token("a")));

        state.Combat!.Log![0].Before!.Tokens.Should().BeEmpty();
    }

    [Fact]
    public void Consistent_log_has_no_repairs()
    {
        var (state, repairs) = MapStateRepair.Repair(State(WithLog([Log("l1"), Log("l2")]), Token("a")));

        (state.Combat!.Log!.Count, repairs).Should().Be((2, 0));
    }

    [Fact]
    public void Missing_log_stays_null()
    {
        MapStateRepair.Repair(State(WithLog(null), Token("a"))).State.Combat!.Log.Should().BeNull();
    }

    [Fact]
    public void LastCombat_without_entries_is_dropped()
    {
        var last = new CombatSnapshot(new CombatState(1, null, []), []);

        var (state, repairs) = MapStateRepair.Repair(State(null, Token("a")) with { LastCombat = last });

        (state.LastCombat, repairs).Should().Be((null, 1));
    }

    [Fact]
    public void LastCombat_with_entries_of_tokens_gone_from_the_map_is_kept_without_repairs()
    {
        var last = new CombatSnapshot(new CombatState(1, "e1", [Entry("e1", "ghost")]), []);

        var (state, repairs) = MapStateRepair.Repair(State(null, Token("a")) with { LastCombat = last });

        (state.LastCombat!.Combat!.Entries.Count, repairs).Should().Be((1, 0));
    }

    [Fact]
    public void LastCombat_with_null_tokens_is_dropped()
    {
        var last = new CombatSnapshot(new CombatState(1, "e1", [Entry("e1", "a")]), null!);

        var (state, repairs) = MapStateRepair.Repair(State(null, Token("a")) with { LastCombat = last });

        (state.LastCombat, repairs).Should().Be((null, 1));
    }

    [Fact]
    public void LastCombat_combat_gets_the_same_repairs_including_its_log()
    {
        var combat = new CombatState(0, "e1", [Entry("e1", "a")], [Log("l1"), Log("l1")]);
        var last = new CombatSnapshot(combat, []);

        var (state, repairs) = MapStateRepair.Repair(State(null, Token("a")) with { LastCombat = last });

        (state.LastCombat!.Combat!.Round, state.LastCombat.Combat.Log!.Count, repairs).Should().Be((1, 1, 2));
    }

    [Fact]
    public void Consistent_LastCombat_has_no_repairs()
    {
        var last = new CombatSnapshot(new CombatState(2, "e1", [Entry("e1", "a")], [Log("l1")]), [new TokenCombatFields("a", 3, null, null, null, null)]);

        var (state, repairs) = MapStateRepair.Repair(State(null, Token("a")) with { LastCombat = last });

        (state.LastCombat!.Combat!.Log!.Count, state.LastCombat.Tokens.Count, repairs).Should().Be((1, 1, 0));
    }

    [Fact]
    public void Log_snapshot_drops_null_tokens_and_null_entries()
    {
        var snap = new CombatSnapshot(new CombatState(1, "e1", [Entry("e1", "a"), null!]), [new TokenCombatFields("a", 1, null, null, null, null), null!]);
        var entry = Log("l1") with { Before = snap };

        var (state, repairs) = MapStateRepair.Repair(State(WithLog([entry]), Token("a")));

        (state.Combat!.Log![0].Before!.Tokens.Count, state.Combat.Log[0].Before!.Combat!.Entries.Count, repairs).Should().Be((1, 1, 2));
    }

    [Fact]
    public void Log_snapshot_combat_loses_its_nested_log()
    {
        var snap = new CombatSnapshot(new CombatState(1, "e1", [Entry("e1", "a")], [Log("inner")]), []);
        var entry = Log("l1") with { After = snap };

        var (state, repairs) = MapStateRepair.Repair(State(WithLog([entry]), Token("a")));

        (state.Combat!.Log![0].After!.Combat!.Log, repairs).Should().Be((null, 1));
    }

    [Fact]
    public void Log_entry_with_null_text_or_id_is_dropped()
    {
        var noText = Log("l2") with { Text = null! };
        var noId = Log("l3") with { Id = null! };

        var (state, repairs) = MapStateRepair.Repair(State(WithLog([Log("l1"), noText, noId]), Token("a")));

        (string.Join(',', state.Combat!.Log!.Select(e => e.Id)), repairs).Should().Be(("l1", 2));
    }

    [Fact]
    public void LastCombat_drops_null_token_fields()
    {
        var last = new CombatSnapshot(new CombatState(1, "e1", [Entry("e1", "a")]), [null!]);

        var (state, repairs) = MapStateRepair.Repair(State(null, Token("a")) with { LastCombat = last });

        (state.LastCombat!.Tokens.Count, repairs).Should().Be((0, 1));
    }
}
