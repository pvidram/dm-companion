using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Tests.GameSystems;

public class BasicCombatRulesTests
{
    private static readonly ICombatRules Rules = BasicCombatRules.Instance;

    [Fact]
    public void Basic_rules_have_no_catalog_death_saves_concentration_or_phases()
    {
        Rules.Conditions.Should().BeEmpty();
        Rules.HasDeathSaves.Should().BeFalse();
        Rules.ConcentrationKey.Should().BeNull();
        Rules.ConcentrationDc(30).Should().BeNull();
        Rules.Phases.Should().BeEmpty();
    }

    [Fact]
    public void Basic_damage_ignores_temp() => Rules.ApplyDamage(10, 5, 3).Should().Be((7, 5));

    [Fact]
    public void Basic_damage_never_goes_below_zero() => Rules.ApplyDamage(4, 5, 50).Should().Be((0, 5));

    [Fact]
    public void Basic_healing_caps_at_max_when_present() => Rules.ApplyHealing(5, 8, 20).Should().Be(8);
}
