using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class StatTests : UiTestContext
{
    [Fact]
    public void Manual_stat_shows_the_pencil_icon()
    {
        var cut = Render<StatBadge>(p => p.Add(x => x.Label, "DES").Add(x => x.Value, "+2").Add(x => x.IsManual, true));
        cut.Find(".ds-stat__manual").GetAttribute("style").Should().Contain("pencil.svg");
    }

    [Fact]
    public void Manual_stat_announces_it_to_screen_readers()
    {
        var cut = Render<StatBadge>(p => p.Add(x => x.Label, "DES").Add(x => x.Value, "+2").Add(x => x.IsManual, true));
        cut.Find(".ds-stat .ds-visually-hidden").TextContent.Should().Be("(valor manual)");
    }

    [Fact]
    public void Computed_stat_has_no_manual_marker()
    {
        var cut = Render<StatBadge>(p => p.Add(x => x.Label, "DES").Add(x => x.Value, "+2"));
        cut.FindAll(".ds-stat__manual").Should().BeEmpty();
    }

    [Fact]
    public void Stat_group_title_is_an_h3()
    {
        var cut = Render<StatGroup>(p => p.Add(x => x.Title, "Salvaciones"));
        cut.Find("h3.ds-stats__heading").TextContent.Should().Be("Salvaciones");
    }

    [Fact]
    public void Stat_group_is_labelled_by_its_title()
    {
        var cut = Render<StatGroup>(p => p.Add(x => x.Title, "Habilidades"));
        cut.Find("section.ds-stats-group").GetAttribute("aria-labelledby").Should().Be(cut.Find("h3").Id);
    }

    [Fact]
    public void Stat_group_renders_its_badges_in_the_stats_grid()
    {
        var cut = Render<StatGroup>(p => p.Add(x => x.Title, "Salvaciones")
            .AddChildContent<StatBadge>(b => b.Add(x => x.Label, "FUE").Add(x => x.Value, "+5")));
        cut.FindAll(".ds-stats > .ds-stat").Should().HaveCount(1);
    }

    [Fact]
    public void Split_stat_group_uses_the_split_grid()
    {
        var cut = Render<StatGroup>(p => p.Add(x => x.Title, "Salvaciones").Add(x => x.Split, true));
        cut.Find(".ds-stats").ClassList.Should().Contain("ds-stats--split");
    }
}
