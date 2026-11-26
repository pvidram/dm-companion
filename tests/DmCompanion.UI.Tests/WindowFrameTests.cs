using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class WindowFrameTests : UiTestContext
{
    [Fact]
    public void Pin_button_reports_the_new_value()
    {
        bool? received = null;
        var cut = Render<PinButton>(p => p
            .Add(x => x.IsPinned, false)
            .Add(x => x.OnToggle, (bool v) => { received = v; }));
        cut.Find("button").Click();
        received.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, "false")]
    [InlineData(true, "true")]
    public void Pin_button_exposes_aria_pressed(bool pinned, string expected)
    {
        var cut = Render<PinButton>(p => p.Add(x => x.IsPinned, pinned));
        cut.Find("button").GetAttribute("aria-pressed").Should().Be(expected);
    }

    [Fact]
    public void Pin_button_label_changes_when_pinned()
    {
        var cut = Render<PinButton>(p => p.Add(x => x.IsPinned, true));
        cut.Find("button").GetAttribute("aria-label").Should().Be("Desanclar");
    }

    [Fact]
    public void Resize_handle_marks_its_edge()
    {
        var cut = Render<ResizeHandle>(p => p.Add(x => x.Edge, "se"));
        cut.Find("div").GetAttribute("data-resize-handle").Should().Be("se");
    }

    [Fact]
    public void Resize_handle_carries_the_edge_modifier_class()
    {
        var cut = Render<ResizeHandle>(p => p.Add(x => x.Edge, "se"));
        cut.Find("div").ClassList.Should().Contain("ds-resize-handle--se");
    }
}
