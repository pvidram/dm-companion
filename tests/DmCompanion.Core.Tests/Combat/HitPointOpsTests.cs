using DmCompanion.Core.Combat;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

public class HitPointOpsTests
{
    private static readonly ICombatRules Rules = Dnd5eCombatRules.Instance;

    private static MapToken Tok(int? current, int? temp = null, DeathSaves? saves = null, params TokenCondition[] conditions) =>
        new("t1", "t1", null, TokenSide.Enemy, 1, 0, 0, 10, 20, current, temp,
            conditions.Length == 0 ? null : conditions, saves);

    private static MapState Map(MapToken t) => MapState.Empty with { Tokens = [t] };

    private static MapToken T(MapState s) => s.Tokens.Single();

    [Fact]
    public void Damage_uses_max_when_current_is_null()
    {
        var r = HitPointOps.Damage(Map(Tok(null)), "t1", 3, 11, Rules);
        T(r.State).HpCurrent.Should().Be(8);
    }

    [Fact]
    public void Damage_spends_temp_first_via_rules()
    {
        var r = HitPointOps.Damage(Map(Tok(10, 5)), "t1", 7, 20, Rules);
        (T(r.State).HpCurrent, T(r.State).HpTemp).Should().Be((8, null));
    }

    [Fact]
    public void Damage_larger_than_temp_and_current_ends_at_zero()
    {
        var r = HitPointOps.Damage(Map(Tok(4, 2)), "t1", 50, 20, Rules);
        (T(r.State).HpCurrent, T(r.State).HpTemp).Should().Be((0, null));
    }

    [Fact]
    public void Damage_without_current_and_max_does_nothing()
    {
        var s = Map(Tok(null));
        HitPointOps.Damage(s, "t1", 3, null, Rules).State.Should().BeSameAs(s);
    }

    [Fact]
    public void Damage_to_concentrating_token_notifies_dc()
    {
        var conc = new TokenCondition("c", Rules.ConcentrationKey, "Concentración", null, null, false);
        var r = HitPointOps.Damage(Map(Tok(30, null, null, conc)), "t1", 22, 30, Rules);
        r.Notices.Should().Equal(new ConcentrationCheck("t1", 11));
    }

    [Fact]
    public void Damage_without_concentration_has_no_notice()
    {
        HitPointOps.Damage(Map(Tok(30)), "t1", 22, 30, Rules).Notices.Should().BeEmpty();
    }

    [Fact]
    public void Zero_or_negative_amounts_do_nothing()
    {
        var s = Map(Tok(10));
        new[]
        {
            HitPointOps.Damage(s, "t1", 0, 20, Rules).State,
            HitPointOps.Damage(s, "t1", -3, 20, Rules).State,
            HitPointOps.Heal(s, "t1", 0, 20, Rules),
            HitPointOps.Heal(s, "t1", -3, 20, Rules),
        }.Should().AllSatisfy(x => x.Should().BeSameAs(s));
    }

    [Fact]
    public void Heal_from_zero_resets_death_saves()
    {
        var s = HitPointOps.Heal(Map(Tok(0, null, new DeathSaves(1, 2))), "t1", 5, 20, Rules);
        (T(s).HpCurrent, T(s).DeathSaves).Should().Be((5, null));
    }

    [Fact]
    public void Heal_caps_at_max()
    {
        T(HitPointOps.Heal(Map(Tok(18)), "t1", 10, 20, Rules)).HpCurrent.Should().Be(20);
    }

    [Fact]
    public void Heal_without_max_adds_freely()
    {
        T(HitPointOps.Heal(Map(Tok(18)), "t1", 10, null, Rules)).HpCurrent.Should().Be(28);
    }

    [Fact]
    public void Heal_does_not_touch_temp_hp()
    {
        T(HitPointOps.Heal(Map(Tok(10, 4)), "t1", 3, 20, Rules)).HpTemp.Should().Be(4);
    }

    [Fact]
    public void SetTempHp_replaces_previous_value()
    {
        T(HitPointOps.SetTempHp(Map(Tok(10, 4)), "t1", 9)).HpTemp.Should().Be(9);
    }

    [Fact]
    public void SetTempHp_zero_clears_it()
    {
        T(HitPointOps.SetTempHp(Map(Tok(10, 4)), "t1", 0)).HpTemp.Should().BeNull();
    }

    [Fact]
    public void SetDeathSaves_clamps_to_0_3()
    {
        T(HitPointOps.SetDeathSaves(Map(Tok(0)), "t1", 7, -2)).DeathSaves.Should().Be(new DeathSaves(3, 0));
    }
}
