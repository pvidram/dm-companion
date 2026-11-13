using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class CheckboxTests : UiTestContext
{
    [Fact]
    public void Checkbox_is_a_native_checkbox_inside_its_label()
    {
        var cut = Render<Checkbox>(p => p.Add(x => x.Label, "Oculto"));
        cut.Find("label.ds-checkbox input[type='checkbox']").Should().NotBeNull();
    }

    [Fact]
    public void Checkbox_shows_its_label()
    {
        var cut = Render<Checkbox>(p => p.Add(x => x.Label, "Oculto"));
        cut.Find("label.ds-checkbox").TextContent.Should().Contain("Oculto");
    }

    [Fact]
    public void Checked_value_renders_checked()
    {
        var cut = Render<Checkbox>(p => p.Add(x => x.Label, "Oculto").Add(x => x.Value, true));
        cut.Find("input").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void Change_emits_the_new_value()
    {
        bool? received = null;
        var cut = Render<Checkbox>(p => p.Add(x => x.Label, "Oculto").Add(x => x.ValueChanged, (bool value) => received = value));
        cut.Find("input").Change(true);
        received.Should().BeTrue();
    }

    [Fact]
    public void Disabled_checkbox_renders_the_disabled_attribute()
    {
        var cut = Render<Checkbox>(p => p.Add(x => x.Label, "Oculto").Add(x => x.Disabled, true));
        cut.Find("input").HasAttribute("disabled").Should().BeTrue();
    }
}
