using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class AttributePassthroughTests : UiTestContext
{
    [Fact]
    public void Extra_attributes_reach_the_button_element()
    {
        var cut = Render<Button>(p => p.AddUnmatched("data-test", "guardar"));
        cut.Find("button").GetAttribute("data-test").Should().Be("guardar");
    }

    [Fact]
    public void Extra_class_is_appended_to_the_parchment_classes()
    {
        var cut = Render<Parchment>(p => p.Add(x => x.Title, "Notas").AddUnmatched("class", "mi-panel"));
        cut.Find("section").ClassList.Should().Contain(["ds-parchment", "mi-panel"]);
    }

    [Fact]
    public void Attributes_set_by_the_component_win_over_extra_ones()
    {
        var cut = Render<RailButton>(p => p
            .Add(x => x.Icon, IconName.Users).Add(x => x.Label, "Jugadores").Add(x => x.IsPressed, true)
            .AddUnmatched("aria-pressed", "false"));
        cut.Find("button").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void Extra_attributes_reach_the_text_field_input()
    {
        var cut = Render<TextField>(p => p.Add(x => x.Label, "Nombre").AddUnmatched("data-test", "nombre"));
        cut.Find("input").GetAttribute("data-test").Should().Be("nombre");
    }
}
