using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class InitiativeTests : UiTestContext
{
    [Theory]
    [InlineData(Faction.Pc, IconName.Shield)]
    [InlineData(Faction.Ally, IconName.Hand)]
    [InlineData(Faction.Enemy, IconName.Swords)]
    public void Each_faction_has_its_own_icon(Faction faction, IconName expected) =>
        FactionVisuals.Icon(faction).Should().Be(expected);

    [Fact]
    public void Token_without_portrait_shows_the_initial()
    {
        var cut = Render<TokenAvatar>(p => p.Add(x => x.Name, "goblin").Add(x => x.Faction, Faction.Enemy));
        cut.Find(".ds-token__initial").TextContent.Should().Be("G");
    }

    [Fact]
    public void Token_ring_follows_the_faction()
    {
        var cut = Render<TokenAvatar>(p => p.Add(x => x.Name, "Aria").Add(x => x.Faction, Faction.Ally));
        cut.Find(".ds-token").ClassList.Should().Contain("ds-token--ally");
    }

    [Fact]
    public void Current_turn_chip_is_marked_with_aria_current()
    {
        var cut = Render<InitiativeChip>(p => p.Add(x => x.Name, "Aria").Add(x => x.Faction, Faction.Pc).Add(x => x.Initiative, 18).Add(x => x.IsCurrent, true));
        cut.Find("button").GetAttribute("aria-current").Should().Be("true");
    }

    [Fact]
    public void Other_chips_have_no_aria_current()
    {
        var cut = Render<InitiativeChip>(p => p.Add(x => x.Name, "Aria").Add(x => x.Faction, Faction.Pc).Add(x => x.Initiative, 18));
        cut.Find("button").HasAttribute("aria-current").Should().BeFalse();
    }

    [Fact]
    public void Chip_label_names_combatant_initiative_and_faction()
    {
        var cut = Render<InitiativeChip>(p => p.Add(x => x.Name, "Goblin").Add(x => x.Faction, Faction.Enemy).Add(x => x.Initiative, 15));
        cut.Find("button").GetAttribute("aria-label").Should().Be("Goblin, iniciativa 15, Enemigo");
    }

    [Fact]
    public void Selecting_a_chip_invokes_on_select()
    {
        var selected = false;
        var cut = Render<InitiativeChip>(p => p.Add(x => x.Name, "Aria").Add(x => x.Faction, Faction.Pc).Add(x => x.Initiative, 18).Add(x => x.OnSelect, () => selected = true));
        cut.Find("button").Click();
        selected.Should().BeTrue();
    }
}
