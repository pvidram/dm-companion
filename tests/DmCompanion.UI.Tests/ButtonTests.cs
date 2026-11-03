using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class ButtonTests : UiTestContext
{
    [Fact]
    public void Button_applies_the_variant_class()
    {
        var cut = Render<Button>(p => p.Add(x => x.Variant, ButtonVariant.Damage).AddChildContent("Daño"));
        cut.Find("button").ClassList.Should().Contain("ds-button--damage");
    }

    [Fact]
    public void Button_invokes_on_click()
    {
        var clicked = false;
        var cut = Render<Button>(p => p.Add(x => x.OnClick, () => clicked = true).AddChildContent("Ok"));
        cut.Find("button").Click();
        clicked.Should().BeTrue();
    }

    [Fact]
    public void Disabled_button_renders_the_disabled_attribute()
    {
        var cut = Render<Button>(p => p.Add(x => x.Disabled, true).AddChildContent("Ok"));
        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Icon_button_exposes_its_label_to_assistive_technology()
    {
        var cut = Render<IconButton>(p => p.Add(x => x.Icon, IconName.X).Add(x => x.Label, "Cerrar"));
        cut.Find("button").GetAttribute("aria-label").Should().Be("Cerrar");
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Rail_button_reflects_pressed_in_aria_pressed(bool pressed, string expected)
    {
        var cut = Render<RailButton>(p => p.Add(x => x.Icon, IconName.ScrollText).Add(x => x.Label, "Notas").Add(x => x.IsPressed, pressed));
        cut.Find("button").GetAttribute("aria-pressed").Should().Be(expected);
    }

    [Fact]
    public void Rail_button_invokes_on_toggle()
    {
        var toggled = false;
        var cut = Render<RailButton>(p => p.Add(x => x.Icon, IconName.Users).Add(x => x.Label, "Jugadores").Add(x => x.OnToggle, () => toggled = true));
        cut.Find("button").Click();
        toggled.Should().BeTrue();
    }
}
