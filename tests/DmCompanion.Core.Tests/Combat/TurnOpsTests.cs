using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

public class TurnOpsTests
{
    private static Func<string> Ids(string prefix = "e")
    {
        var n = 0;
        return () => $"{prefix}{++n}";
    }

    private static MapToken Tok(string id, params TokenCondition[] conditions) =>
        new(id, id, null, TokenSide.Enemy, 1, 0, 0, 10, 10, Conditions: conditions.Length == 0 ? null : conditions);

    private static MapState Map(params MapToken[] tokens) =>
        MapState.Empty with { Tokens = tokens.Length == 0 ? [Tok("t1"), Tok("t2"), Tok("t3"), Tok("t4")] : tokens };

    private static TokenCondition Cond(string id, string entryId, int left, bool expired = false) =>
        new(id, null, id, null, new ConditionDuration(entryId, left), expired);

    private static MapState Combat(MapState s, params (string Token, int? Init)[] entries) =>
        TurnOps.Start(s, entries.Select(e => new InitiativeEntry(e.Token, e.Init)).ToList(), Ids()).State;

    private static IEnumerable<string> Order(MapState s) => s.Combat!.Entries.Select(e => e.TokenId);

    // Entradas: e1=t1(18), e2=t2(15), e3=t3(9)
    private static MapState Three() => Combat(Map(), ("t1", 18), ("t2", 15), ("t3", 9));

    [Fact]
    public void Start_orders_by_initiative_descending()
    {
        var s = Combat(Map(), ("t1", 9), ("t2", 18), ("t3", 15));
        Order(s).Should().Equal("t2", "t3", "t1");
    }

    [Fact]
    public void Start_keeps_entry_order_on_ties()
    {
        var s = Combat(Map(), ("t1", 12), ("t2", 12), ("t3", 12));
        Order(s).Should().Equal("t1", "t2", "t3");
    }

    [Fact]
    public void Start_puts_entries_without_number_last()
    {
        var s = Combat(Map(), ("t1", null), ("t2", 3), ("t3", null), ("t4", 8));
        Order(s).Should().Equal("t4", "t2", "t1", "t3");
    }

    [Fact]
    public void Start_sets_round_1_and_current_first()
    {
        var s = Combat(Map(), ("t1", 5), ("t2", 20));
        (s.Combat!.Round, s.Combat.CurrentEntryId, s.Combat.Entries[0].TokenId)
            .Should().Be((1, s.Combat.Entries[0].Id, "t2"));
    }

    [Fact]
    public void Start_counts_down_the_first_entry_durations()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("c1", "e1", 1)));
        var r = TurnOps.Start(map, [new("t1", 18), new("t2", 5)], Ids());
        r.State.Tokens[1].ConditionList[0].Should().BeEquivalentTo(Cond("c1", "e1", 0, expired: true));
        r.Notices.Should().BeEquivalentTo(new CombatNotice[] { new ConditionExpired("t2", "c1") });
    }

    [Fact]
    public void Start_with_no_entries_does_nothing()
    {
        var map = Map();
        var r = TurnOps.Start(map, [], Ids());
        r.State.Should().BeSameAs(map);
        r.Notices.Should().BeEmpty();
    }

    [Fact]
    public void AddEntry_goes_after_the_last_entry_with_greater_or_equal_initiative()
    {
        var s = TurnOps.AddEntry(Three(), new("t4", 15), Ids("n"));
        Order(s).Should().Equal("t1", "t2", "t4", "t3");
    }

    [Fact]
    public void AddEntry_with_highest_initiative_goes_first()
    {
        var s = TurnOps.AddEntry(Three(), new("t4", 20), Ids("n"));
        Order(s).Should().Equal("t4", "t1", "t2", "t3");
    }

    [Fact]
    public void AddEntry_with_unnumbered_entries_present_keeps_numbered_before_them()
    {
        var s = Combat(Map(), ("t1", 18), ("t2", null));
        var orders = new[]
        {
            Order(TurnOps.AddEntry(s, new("t4", 20), Ids("n"))),
            Order(TurnOps.AddEntry(s, new("t4", 10), Ids("n"))),
        };
        orders.Should().BeEquivalentTo(new[] { new[] { "t4", "t1", "t2" }, new[] { "t1", "t4", "t2" } },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void AddEntry_without_number_goes_last()
    {
        var s = TurnOps.AddEntry(Three(), new("t4", null), Ids("n"));
        Order(s).Should().Equal("t1", "t2", "t3", "t4");
    }

    [Fact]
    public void MoveEntry_reorders_without_changing_initiative()
    {
        var s = TurnOps.MoveEntry(Three(), "e3", 0);
        s.Combat!.Entries.Select(e => (e.TokenId, e.Initiative)).Should().Equal(("t3", 9), ("t1", 18), ("t2", 15));
    }

    [Fact]
    public void MoveEntry_clamps_index()
    {
        var orders = new[] { Order(TurnOps.MoveEntry(Three(), "e1", 99)), Order(TurnOps.MoveEntry(Three(), "e3", -4)) };
        orders.Should().BeEquivalentTo(new[] { new[] { "t2", "t3", "t1" }, new[] { "t3", "t1", "t2" } },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void NextTurn_advances_to_next_entry()
    {
        var s = TurnOps.NextTurn(Three()).State;
        (s.Combat!.CurrentEntryId, s.Combat.Round).Should().Be(("e2", 1));
    }

    [Fact]
    public void NextTurn_wraps_and_increments_round()
    {
        var s = Three();
        for (var i = 0; i < 3; i++) s = TurnOps.NextTurn(s).State;
        (s.Combat!.CurrentEntryId, s.Combat.Round).Should().Be(("e1", 2));
    }

    [Fact]
    public void NextTurn_decrements_durations_anchored_to_the_new_entry_only()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e2", 3), Cond("b", "e3", 3)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9));
        var next = TurnOps.NextTurn(s).State;
        next.Tokens[1].ConditionList.Select(c => c.Duration!.TurnsLeft).Should().Equal(2, 3);
    }

    [Fact]
    public void NextTurn_marks_expired_and_notifies_at_zero()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e3", 1)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9));
        var r = TurnOps.NextTurn(TurnOps.NextTurn(s).State);
        r.State.Tokens[1].ConditionList[0].Should().BeEquivalentTo(Cond("a", "e3", 0, expired: true));
        r.Notices.Should().BeEquivalentTo(new CombatNotice[] { new ConditionExpired("t2", "a") });
    }

    [Fact]
    public void NextTurn_does_not_touch_already_expired_conditions()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e2", 0, expired: true)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9));
        var r = TurnOps.NextTurn(s);
        r.State.Tokens[1].ConditionList[0].Should().BeEquivalentTo(Cond("a", "e2", 0, expired: true));
        r.Notices.Should().BeEmpty();
    }

    [Fact]
    public void SetCurrent_changes_turn_without_counting_down_or_changing_round()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e1", 2)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9));
        s = TurnOps.NextTurn(s).State;
        var after = TurnOps.SetCurrent(s, "e1");
        (after.Combat!.CurrentEntryId, after.Combat.Round, after.Tokens[1].ConditionList[0].Duration!.TurnsLeft)
            .Should().Be(("e1", 1, 1));
    }

    [Fact]
    public void RemoveEntry_of_current_passes_turn_to_next_without_counting_down()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e2", 2)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9));
        var after = TurnOps.RemoveEntry(s, "e1");
        after.Combat!.CurrentEntryId.Should().Be("e2");
        after.Tokens[1].ConditionList[0].Duration!.TurnsLeft.Should().Be(2);
    }

    [Fact]
    public void RemoveEntry_reanchors_durations_to_the_affected_token_entry()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e1", 2)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9));
        var after = TurnOps.RemoveEntry(s, "e1");
        after.Tokens[1].ConditionList[0].Duration.Should().Be(new ConditionDuration("e2", 1)); // 2 menos el inicio de turno de e1 al empezar
    }

    [Fact]
    public void RemoveEntry_drops_duration_when_affected_token_has_no_entry()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e1", 2)), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t3", 9));
        var after = TurnOps.RemoveEntry(s, "e1");
        after.Tokens[1].ConditionList[0].Duration.Should().BeNull();
    }

    [Fact]
    public void RemoveEntry_of_last_entry_ends_combat()
    {
        var s = Combat(Map(), ("t1", 18));
        TurnOps.RemoveEntry(s, "e1").Combat.Should().BeNull();
    }

    [Fact]
    public void RemoveEntry_of_current_last_entry_passes_to_first_and_increments_round_without_counting_down()
    {
        var map = Map(Tok("t1", Cond("a", "e1", 3)), Tok("t2"), Tok("t3"));
        var s = Combat(map, ("t1", 18), ("t2", 15), ("t3", 9)); // a: 3 -> 2 al empezar
        s = TurnOps.SetCurrent(s, "e3");
        var after = TurnOps.RemoveEntry(s, "e3");
        (after.Combat!.CurrentEntryId, after.Combat.Round, after.Tokens[0].ConditionList[0].Duration!.TurnsLeft)
            .Should().Be(("e1", 2, 2));
    }

    [Fact]
    public void MoveEntry_with_unknown_id_returns_same_state()
    {
        var s = Three();
        TurnOps.MoveEntry(s, "zz", 0).Should().BeSameAs(s);
    }

    [Fact]
    public void SetCurrent_with_unknown_id_returns_same_state()
    {
        var s = Three();
        TurnOps.SetCurrent(s, "zz").Should().BeSameAs(s);
    }

    [Fact]
    public void RemoveEntry_unknown_returns_same_state()
    {
        var s = Three();
        TurnOps.RemoveEntry(s, "zz").Should().BeSameAs(s);
    }

    [Fact]
    public void End_removes_combat_durations_expired_flags_and_death_saves_but_keeps_hp_and_temp_hp()
    {
        var t2 = Tok("t2", Cond("a", "e1", 2), Cond("b", "e1", 0, expired: true)) with
        {
            HpCurrent = 3, HpTemp = 5, DeathSaves = new DeathSaves(1, 2),
        };
        var s = Combat(Map(Tok("t1"), t2), ("t1", 18), ("t2", 9));
        var after = TurnOps.End(s);
        after.Combat.Should().BeNull();
        after.Tokens[1].Should().BeEquivalentTo(t2 with
        {
            DeathSaves = null,
            Conditions = [Cond("a", "e1", 0) with { Duration = null }, Cond("b", "e1", 0) with { Duration = null }],
        });
    }

    [Fact]
    public void Operations_without_combat_return_the_same_state()
    {
        var map = Map();
        var results = new[]
        {
            TurnOps.End(map), TurnOps.NextTurn(map).State, TurnOps.AddEntry(map, new("t1", 3), Ids()),
        };
        results.Should().OnlyContain(r => ReferenceEquals(r, map));
    }

    private static MapState Kill(MapState s, params string[] tokenIds) =>
        tokenIds.Aggregate(s, DeathOps.MarkDead);

    [Fact]
    public void NextTurn_skips_dead_entries_without_counting_down_their_durations()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e2", 2)), Tok("t3", Cond("b", "e3", 2)));
        var s = Kill(Combat(map, ("t1", 18), ("t2", 15), ("t3", 9)), "t2");
        var after = TurnOps.NextTurn(s).State;
        (after.Combat!.CurrentEntryId, after.Tokens[1].ConditionList[0].Duration!.TurnsLeft, after.Tokens[2].ConditionList[0].Duration!.TurnsLeft)
            .Should().Be(("e3", 2, 1));
    }

    [Fact]
    public void NextTurn_skipping_dead_across_the_end_increments_round()
    {
        var s = Combat(Map(Tok("t1"), Tok("t2"), Tok("t3")), ("t1", 18), ("t2", 15), ("t3", 9));
        s = TurnOps.SetCurrent(Kill(s, "t3"), "e2");
        var after = TurnOps.NextTurn(s).State;
        (after.Combat!.CurrentEntryId, after.Combat.Round).Should().Be(("e1", 2));
    }

    [Fact]
    public void NextTurn_with_only_the_current_entry_alive_comes_back_to_it_raising_the_round_and_counting_down()
    {
        var map = Map(Tok("t1", Cond("a", "e1", 3)), Tok("t2"), Tok("t3")); // Start ya descuenta el turno de e1: 3 pasa a 2
        var s = Kill(Combat(map, ("t1", 18), ("t2", 15), ("t3", 9)), "t2", "t3");
        var after = TurnOps.NextTurn(s).State;
        (after.Combat!.CurrentEntryId, after.Combat.Round, after.Tokens[0].ConditionList[0].Duration!.TurnsLeft).Should().Be(("e1", 2, 1));
    }

    [Fact]
    public void NextTurn_with_every_entry_dead_returns_the_same_state()
    {
        var s = Kill(Combat(Map(Tok("t1"), Tok("t2")), ("t1", 18), ("t2", 15)), "t1", "t2");
        TurnOps.NextTurn(s).State.Should().BeSameAs(s);
    }

    [Fact]
    public void Start_puts_the_turn_on_the_first_living_entry()
    {
        var s = Combat(Map(Tok("t1"), Tok("t2")), ("t1", 18), ("t2", 15));
        var dead = Kill(s, "t1");
        var restarted = TurnOps.Start(dead, [new("t1", 18), new("t2", 15)], Ids("n")).State;
        restarted.Combat!.CurrentEntryId.Should().Be("n2");
    }

    [Fact]
    public void Start_with_every_entry_dead_puts_the_turn_on_the_first()
    {
        var map = Kill(Map(Tok("t1"), Tok("t2")), "t1", "t2");
        var s = TurnOps.Start(map, [new("t1", 18), new("t2", 15)], Ids()).State;
        s.Combat!.CurrentEntryId.Should().Be("e1");
    }

    [Fact]
    public void SetCurrent_to_a_dead_entry_does_not_count_down()
    {
        var map = Map(Tok("t1"), Tok("t2", Cond("a", "e2", 2)));
        var s = Kill(Combat(map, ("t1", 18), ("t2", 15)), "t2");
        TurnOps.SetCurrent(s, "e2").Tokens[1].ConditionList[0].Duration!.TurnsLeft.Should().Be(2);
    }

    [Fact]
    public void Marking_the_current_entry_dead_does_not_move_the_turn()
    {
        var s = Combat(Map(Tok("t1"), Tok("t2")), ("t1", 18), ("t2", 15));
        DeathOps.MarkDead(s, "t1").Combat!.CurrentEntryId.Should().Be("e1");
    }

    [Fact]
    public void NextTurn_treats_entries_without_a_token_on_the_map_as_alive()
    {
        var s = Combat(Map(Tok("t1"), Tok("t2")), ("t1", 18), ("t2", 15));
        s = s with { Tokens = s.Tokens.Where(t => t.Id != "t2").ToList() };
        TurnOps.NextTurn(s).State.Combat!.CurrentEntryId.Should().Be("e2");
    }
}
