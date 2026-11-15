using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

public class DeathOpsTests
{
    private static MapToken Tok(int? hp = 7, DeathSaves? saves = null, bool? dead = null) =>
        new("t1", "t1", null, TokenSide.Pc, 1, 0, 0, 10, 20, hp, null, null, saves, dead);

    private static MapState Map(MapToken t) => MapState.Empty with { Tokens = [t] };

    [Fact]
    public void MarkDead_sets_dead_and_keeps_hp()
    {
        var t = DeathOps.MarkDead(Map(Tok()), "t1").Tokens.Single();
        (t.Dead, t.HpCurrent).Should().Be((true, 7));
    }

    [Fact]
    public void Revive_clears_dead_and_keeps_hp_and_death_saves()
    {
        var saves = new DeathSaves(1, 2);
        var t = DeathOps.Revive(Map(Tok(0, saves, true)), "t1").Tokens.Single();
        (t.Dead, t.HpCurrent, t.DeathSaves).Should().Be((null, 0, saves));
    }

    [Fact]
    public void MarkDead_on_a_dead_token_returns_the_same_state()
    {
        var s = Map(Tok(dead: true));
        DeathOps.MarkDead(s, "t1").Should().BeSameAs(s);
    }

    [Fact]
    public void Revive_on_a_living_token_returns_the_same_state()
    {
        var s = Map(Tok());
        DeathOps.Revive(s, "t1").Should().BeSameAs(s);
    }

    [Fact]
    public void MarkDead_on_a_missing_token_returns_the_same_state()
    {
        var s = Map(Tok());
        DeathOps.MarkDead(s, "nope").Should().BeSameAs(s);
    }
}
