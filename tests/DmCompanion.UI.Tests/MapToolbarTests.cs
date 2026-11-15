using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class MapToolbarTests : UiTestContext
{
    [Fact]
    public void Toolbar_is_a_group_named_by_its_label()
    {
        var cut = Render<MapToolbar>(p => p.Add(x => x.Label, "Herramientas del mapa"));
        cut.Find("[role='group'].ds-map-toolbar").GetAttribute("aria-label").Should().Be("Herramientas del mapa");
    }

    [Fact]
    public void Toolbar_sits_on_the_right_edge_by_default()
    {
        var cut = Render<MapToolbar>(p => p.Add(x => x.Label, "Herramientas"));
        cut.Find(".ds-map-toolbar").ClassList.Should().Contain("ds-map-toolbar--right");
    }

    [Fact]
    public void Toolbar_can_sit_on_the_left_edge()
    {
        var cut = Render<MapToolbar>(p => p.Add(x => x.Label, "Herramientas").Add(x => x.Edge, MapToolbarEdge.Left));
        cut.Find(".ds-map-toolbar").ClassList.Should().Contain("ds-map-toolbar--left");
    }

    [Fact]
    public void Toolbar_can_sit_on_the_bottom_edge()
    {
        var cut = Render<MapToolbar>(p => p.Add(x => x.Label, "Herramientas").Add(x => x.Edge, MapToolbarEdge.Bottom));
        cut.Find(".ds-map-toolbar").ClassList.Should().Contain("ds-map-toolbar--bottom");
    }

    [Fact]
    public void Toolbar_renders_its_buttons()
    {
        var cut = Render<MapToolbar>(p => p.Add(x => x.Label, "Herramientas")
            .AddChildContent<MapToolbarGroup>(g => g.AddChildContent<IconButton>(b => b.Add(x => x.Icon, IconName.Ruler).Add(x => x.Label, "Regla"))));
        cut.Find(".ds-map-toolbar__group button").GetAttribute("aria-label").Should().Be("Regla");
    }

    [Fact]
    public void Group_with_label_is_named_by_it()
    {
        var cut = Render<MapToolbarGroup>(p => p.Add(x => x.Label, "Zoom"));
        cut.Find(".ds-map-toolbar__group").GetAttribute("aria-label").Should().Be("Zoom");
    }

    [Fact]
    public void Group_without_label_has_no_aria_label()
    {
        var cut = Render<MapToolbarGroup>();
        cut.Find(".ds-map-toolbar__group").HasAttribute("aria-label").Should().BeFalse();
    }
}
