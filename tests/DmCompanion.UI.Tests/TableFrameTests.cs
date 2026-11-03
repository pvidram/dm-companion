using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class TableFrameTests : UiTestContext
{
    [Fact]
    public void Tv_mode_adds_the_tv_class_to_the_root()
    {
        var cut = Render<TableFrame>(p => p.Add(x => x.IsTv, true).Add(x => x.Map, "<canvas></canvas>"));
        cut.Find(".ds-frame").ClassList.Should().Contain("tv");
    }

    [Fact]
    public void Frame_without_rail_uses_the_no_rail_layout()
    {
        var cut = Render<TableFrame>(p => p.Add(x => x.Map, "<canvas></canvas>"));
        cut.Find(".ds-frame").ClassList.Should().Contain("ds-frame--no-rail");
    }

    [Fact]
    public void Overlays_are_rendered_inside_the_map_area()
    {
        var cut = Render<TableFrame>(p => p
            .Add(x => x.Map, "<canvas></canvas>")
            .Add(x => x.Overlays, "<section class='probe'></section>"));
        cut.FindAll(".ds-frame__map .probe").Should().HaveCount(1);
    }

    [Fact]
    public void Dialogs_are_rendered_outside_the_map_area()
    {
        var cut = Render<TableFrame>(p => p
            .Add(x => x.Map, "<canvas></canvas>")
            .Add(x => x.Dialogs, "<div class='probe'></div>"));
        cut.FindAll(".ds-frame > .probe").Should().HaveCount(1);
    }
}
