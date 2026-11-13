using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class TokenAvatarTests : UiTestContext
{
    [Fact]
    public void Hidden_token_shows_the_eye_off_badge()
    {
        var cut = Render<TokenAvatar>(p => p.Add(x => x.Name, "Goblin").Add(x => x.Faction, Faction.Enemy).Add(x => x.IsHidden, true));
        cut.Find(".ds-token__hidden-badge .ds-icon").GetAttribute("style").Should().Contain("eye-off.svg");
    }

    [Fact]
    public void Hidden_token_announces_it_to_screen_readers()
    {
        var cut = Render<TokenAvatar>(p => p.Add(x => x.Name, "Goblin").Add(x => x.Faction, Faction.Enemy).Add(x => x.IsHidden, true));
        cut.Find(".ds-token .ds-visually-hidden").TextContent.Should().Be("(oculto)");
    }

    [Fact]
    public void Hidden_token_has_the_hidden_modifier()
    {
        var cut = Render<TokenAvatar>(p => p.Add(x => x.Name, "Goblin").Add(x => x.Faction, Faction.Enemy).Add(x => x.IsHidden, true));
        cut.Find(".ds-token").ClassList.Should().Contain("ds-token--hidden");
    }

    [Fact]
    public void Visible_token_has_no_hidden_badge()
    {
        var cut = Render<TokenAvatar>(p => p.Add(x => x.Name, "Aria").Add(x => x.Faction, Faction.Pc));
        cut.FindAll(".ds-token__hidden-badge").Should().BeEmpty();
    }
}
