using DmCompanion.Core.Maps;
using DmCompanion.Screens.Services;
using DmCompanion.UI.Components;

namespace DmCompanion.Screens.Tests;

public sealed class TokenSideMappingTests
{
    [Theory]
    [InlineData(TokenSide.Pc, Faction.Pc)]
    [InlineData(TokenSide.Ally, Faction.Ally)]
    [InlineData(TokenSide.Enemy, Faction.Enemy)]
    public void Maps_each_side_to_its_faction(TokenSide side, Faction faction)
    {
        TokenSideMapping.ToFaction(side).Should().Be(faction);
        TokenSideMapping.ToSide(faction).Should().Be(side);
    }

    [Fact]
    public void Mapping_is_bijective()
    {
        Enum.GetValues<TokenSide>().Select(TokenSideMapping.ToFaction).Should().OnlyHaveUniqueItems()
            .And.HaveCount(Enum.GetValues<Faction>().Length);
        Enum.GetValues<Faction>().Select(f => TokenSideMapping.ToFaction(TokenSideMapping.ToSide(f)))
            .Should().Equal(Enum.GetValues<Faction>());
    }
}
