using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.GameSystems;

public class GenericSystemTests
{
    private static StatValues Evaluate() => new GenericSystem("pathfinder").Evaluate(
        FrontmatterParser.Parse("---\nstats:\n  foo:\n    bar: 3\n    baz: hola\n  qux: 7\n---\n").Fields);

    [Fact]
    public void Evaluate_lists_leaf_keys_in_yaml_order() =>
        Evaluate().ById.Keys.Should().Equal("stats.foo.bar", "stats.foo.baz", "stats.qux");

    [Fact]
    public void Evaluate_shows_values_as_text() =>
        Evaluate().ById["stats.foo.baz"].Display.Should().Be("hola");

    [Fact]
    public void Evaluate_warns_about_unknown_system() =>
        Evaluate().Warnings.Should().Equal("Sistema «pathfinder» desconocido: se muestra la vista genérica.");

    [Fact]
    public void StatsTemplate_is_empty_block() =>
        new GenericSystem("x").StatsTemplate(EntityType.Npc).Should().Be("stats: {}\n");

    private static int? GenericMax(string yaml)
    {
        var system = new GenericSystem("pathfinder");
        return system.HitPoints.Max(system.Evaluate(FrontmatterParser.Parse(yaml).Fields));
    }

    [Fact]
    public void HitPoints_max_is_stats_hp() => GenericMax("---\nstats:\n  hp: 22\n---\n").Should().Be(22);

    [Fact]
    public void HitPoints_max_is_null_without_stats_hp() => GenericMax("---\nstats:\n  qux: 7\n---\n").Should().BeNull();

    [Fact]
    public void HitPoints_max_is_null_when_hp_is_not_a_number() => GenericMax("---\nstats:\n  hp: mucho\n---\n").Should().BeNull();

    [Fact]
    public void HitPoints_label_is_HP() => new GenericSystem("x").HitPoints.Label.Should().Be("HP");

    [Fact] public void Generic_uses_no_srd_pack() => new GenericSystem("x").GlossaryPacks.Should().BeEmpty();
}
