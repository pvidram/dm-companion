using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Tests;

public class AbilityModifierTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(15, 2)]
    [InlineData(20, 5)]
    public void Of_follows_5e_formula(int score, int expected) =>
        AbilityModifier.Of(score).Should().Be(expected);
}
