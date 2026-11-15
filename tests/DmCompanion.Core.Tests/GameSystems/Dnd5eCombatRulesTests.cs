using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Tests.GameSystems;

public class Dnd5eCombatRulesTests
{
    private static readonly ICombatRules Rules = Dnd5eCombatRules.Instance;

    [Fact]
    public void Dnd5e_catalog_has_the_srd_conditions_in_spanish() =>
        Rules.Conditions.Select(c => (c.Key, c.Label, c.MaxLevel)).Should().Equal(
            ("grappled", "Agarrado", (int?)null), ("restrained", "Apresado", null), ("frightened", "Asustado", null),
            ("stunned", "Aturdido", null), ("blinded", "Cegado", null), ("prone", "Derribado", null),
            ("deafened", "Ensordecido", null), ("poisoned", "Envenenado", null), ("charmed", "Hechizado", null),
            ("incapacitated", "Incapacitado", null), ("unconscious", "Inconsciente", null), ("invisible", "Invisible", null),
            ("paralyzed", "Paralizado", null), ("petrified", "Petrificado", null), ("exhaustion", "Agotamiento", 6),
            ("concentration", "Concentración", null));

    [Fact]
    public void Dnd5e_damage_spends_temp_hp_first() => Rules.ApplyDamage(10, 5, 3).Should().Be((10, 2));

    [Fact]
    public void Dnd5e_damage_overflows_from_temp_to_current() => Rules.ApplyDamage(10, 5, 8).Should().Be((7, 0));

    [Fact]
    public void Dnd5e_damage_never_goes_below_zero() => Rules.ApplyDamage(4, 0, 50).Should().Be((0, 0));

    [Fact]
    public void Dnd5e_non_positive_damage_changes_nothing() => Rules.ApplyDamage(4, 2, 0).Should().Be((4, 2));

    [Fact]
    public void Dnd5e_healing_caps_at_max() => Rules.ApplyHealing(5, 11, 20).Should().Be(11);

    [Fact]
    public void Healing_without_max_has_no_cap() => Rules.ApplyHealing(5, null, 20).Should().Be(25);

    [Fact]
    public void Healing_with_non_positive_amount_changes_nothing() => Rules.ApplyHealing(5, 11, 0).Should().Be(5);

    [Theory]
    [InlineData(1, 10)]
    [InlineData(21, 10)]
    [InlineData(22, 11)]
    [InlineData(70, 30)]
    [InlineData(200, 30)]
    public void Dnd5e_concentration_dc(int damage, int dc) => Rules.ConcentrationDc(damage).Should().Be(dc);

    [Fact]
    public void Dnd5e_zero_damage_has_no_concentration_dc() => Rules.ConcentrationDc(0).Should().BeNull();

    [Fact]
    public void Dnd5e_has_death_saves_and_concentration_key()
    {
        Rules.HasDeathSaves.Should().BeTrue();
        Rules.ConcentrationKey.Should().Be("concentration");
        Rules.Phases.Should().BeEmpty();
    }

    [Theory]
    [InlineData(20, 20, "Ileso")]
    [InlineData(11, 20, "Herido")]
    [InlineData(10, 20, "Malherido")]
    [InlineData(1, 20, "Malherido")]
    [InlineData(0, 20, null)]
    [InlineData(5, 0, null)]
    public void Dnd5e_health_label_follows_thresholds(int current, int max, string? expected) =>
        Rules.HealthLabel(current, max).Should().Be(expected);

    [Fact]
    public void Dnd5e_health_label_without_max_is_null() => Rules.HealthLabel(5, null).Should().BeNull();
}
