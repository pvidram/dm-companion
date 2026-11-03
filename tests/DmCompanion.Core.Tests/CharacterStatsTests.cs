using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class CharacterStatsTests
{
    private const string Yaml = """
        ---
        stats:
          ac: 15
          hp: 52
          speed: 30
          initiative_bonus: 2
          abilities: { str: 14, dex: 16, con: 12, int: 10, wis: 13, cha: 8 }
        ---
        """;

    private static CharacterStats Parse(string content) =>
        CharacterStats.FromFrontmatter(FrontmatterParser.Parse(content).Fields);

    [Fact]
    public void FromFrontmatter_reads_core_stats() =>
        Parse(Yaml).Should().Match<CharacterStats>(s =>
            s.ArmorClass == 15 && s.HitPoints == 52 && s.Speed == 30 && s.InitiativeBonus == 2);

    [Fact]
    public void FromFrontmatter_reads_ability_scores() =>
        Parse(Yaml).AbilityScores[Ability.Dex].Should().Be(16);

    [Fact]
    public void FromFrontmatter_without_stats_returns_nulls()
    {
        var stats = Parse("---\nname: x\n---\n");
        (stats.ArmorClass, stats.AbilityScores.Count).Should().Be(((int?)null, 0));
    }

    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(15, 2)]
    [InlineData(20, 5)]
    public void Modifier_follows_5e_formula(int score, int expected) =>
        CharacterStats.Modifier(score).Should().Be(expected);
}
