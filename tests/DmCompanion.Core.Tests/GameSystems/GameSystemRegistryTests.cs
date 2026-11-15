using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Tests.GameSystems;

public class GameSystemRegistryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DND5E")]
    public void Get_resolves_dnd5e(string? id) =>
        GameSystemRegistry.Default.Get(id).Id.Should().Be("dnd5e");

    [Fact]
    public void Get_unknown_system_is_not_known() =>
        GameSystemRegistry.Default.Get("pathfinder").IsKnown.Should().BeFalse();

    [Fact]
    public void Get_unknown_system_keeps_its_id() =>
        GameSystemRegistry.Default.Get("pathfinder").Id.Should().Be("pathfinder");

    [Fact]
    public void All_lists_dnd5e_and_cosmere() =>
        GameSystemRegistry.Default.All.Select(s => s.Id).Should().Equal("dnd5e", "cosmere");

    [Theory]
    [InlineData("dnd5e", typeof(Dnd5eCombatRules))]
    [InlineData("cosmere", typeof(BasicCombatRules))]
    [InlineData("pathfinder", typeof(BasicCombatRules))]
    public void Each_system_exposes_its_combat_rules(string id, Type expected) =>
        GameSystemRegistry.Default.Get(id).Combat.Should().BeOfType(expected);
}

public class StatFormatTests
{
    [Theory]
    [InlineData(-1, "−1")]
    [InlineData(0, "+0")]
    [InlineData(3, "+3")]
    public void Signed_formats_with_typographic_minus(int value, string expected) =>
        StatFormat.Signed(value).Should().Be(expected);

    [Fact]
    public void Plain_null_is_a_dash() => StatFormat.Plain(null).Should().Be("—");
}
