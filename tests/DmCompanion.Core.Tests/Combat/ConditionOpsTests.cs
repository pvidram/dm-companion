using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

public class ConditionOpsTests
{
    private static MapToken Tok(string id, params TokenCondition[] conditions) =>
        new(id, id, null, TokenSide.Enemy, 1, 0, 0, 10, 10, Conditions: conditions.Length == 0 ? null : conditions);

    private static MapState Map(params TokenCondition[] t1Conditions) =>
        MapState.Empty with { Tokens = [Tok("t1", t1Conditions), Tok("t2")] };

    // Entradas: e1=t1, e2=t2
    private static MapState WithCombat(MapState s) =>
        TurnOps.Start(s, [new InitiativeEntry("t1", 18), new InitiativeEntry("t2", 10)], new Counter().Next).State;

    private sealed class Counter
    {
        private int _n;
        public string Next() => $"e{++_n}";
    }

    private static Func<string> Id(string id) => () => id;

    private static ConditionDraft Draft(string label = "Hechizado", string? key = null, int? level = null, int? rounds = null, string? anchor = null) =>
        new(key, label, level, rounds, anchor);

    private static IReadOnlyList<TokenCondition> Of(MapState s, string tokenId = "t1") =>
        s.Tokens.Single(t => t.Id == tokenId).ConditionList;

    [Fact]
    public void Add_without_rounds_has_no_duration()
    {
        var s = ConditionOps.Add(WithCombat(Map()), "t1", Draft(), Id("c1"));
        Of(s).Single().Should().Be(new TokenCondition("c1", null, "Hechizado", null, null, false));
    }

    [Fact]
    public void Add_with_rounds_anchors_to_the_token_entry_by_default()
    {
        var s = ConditionOps.Add(WithCombat(Map()), "t1", Draft(rounds: 3), Id("c1"));
        Of(s).Single().Duration.Should().Be(new ConditionDuration("e1", 3));
    }

    [Fact]
    public void Add_with_rounds_and_explicit_anchor_uses_it()
    {
        var s = ConditionOps.Add(WithCombat(Map()), "t1", Draft(rounds: 2, anchor: "e2"), Id("c1"));
        Of(s).Single().Duration.Should().Be(new ConditionDuration("e2", 2));
    }

    [Fact]
    public void Add_with_rounds_without_combat_ignores_rounds()
    {
        var s = ConditionOps.Add(Map(), "t1", Draft(rounds: 3), Id("c1"));
        Of(s).Single().Duration.Should().BeNull();
    }

    [Fact]
    public void Add_with_blank_label_and_no_key_does_nothing()
    {
        var s = Map();
        ConditionOps.Add(s, "t1", Draft(label: "  "), Id("c1")).Should().BeSameAs(s);
    }

    [Fact]
    public void Level_is_clamped_to_at_least_1()
    {
        var s = ConditionOps.Add(Map(), "t1", Draft(key: "exhaustion", level: 0), Id("c1"));
        Of(s).Single().Level.Should().Be(1);
    }

    [Fact]
    public void Update_changes_level_duration_and_anchor()
    {
        var start = ConditionOps.Add(WithCombat(Map()), "t1", Draft(key: "exhaustion", level: 1, rounds: 3), Id("c1"));
        var s = ConditionOps.Update(start, "t1", "c1", Draft(key: "exhaustion", level: 4, rounds: 5, anchor: "e2"));
        (Of(s).Single().Level, Of(s).Single().Duration).Should().Be((4, new ConditionDuration("e2", 5)));
    }

    [Fact]
    public void Remove_removes_only_that_condition()
    {
        var s = Map(new TokenCondition("a", null, "A", null, null, false), new TokenCondition("b", null, "B", null, null, false));
        ConditionOps.Remove(s, "t1", "a").Tokens[0].ConditionList.Select(c => c.Id).Should().Equal("b");
    }

    [Fact]
    public void KeepExpired_without_rounds_clears_duration_and_expired()
    {
        var s = Map(new TokenCondition("a", null, "A", null, new ConditionDuration("e1", 0), true));
        var r = ConditionOps.KeepExpired(WithCombat(s), "t1", "a", null);
        (Of(r).Single().Duration, Of(r).Single().Expired).Should().Be((null, false));
    }

    [Fact]
    public void KeepExpired_with_rounds_restarts_duration()
    {
        var s = Map(new TokenCondition("a", null, "A", null, new ConditionDuration("e1", 0), true));
        var r = ConditionOps.KeepExpired(WithCombat(s), "t1", "a", 2);
        (Of(r).Single().Duration, Of(r).Single().Expired).Should().Be((new ConditionDuration("e1", 2), false));
    }
}
