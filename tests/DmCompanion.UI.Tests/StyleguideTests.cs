using DmCompanion.UI.Pages;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class StyleguideTests : UiTestContext
{
    [Fact]
    public void Styleguide_renders_one_parchment_per_sample()
    {
        var cut = Render<Styleguide>();
        cut.FindAll(".ds-parchment").Should().HaveCount(5);
    }

    [Fact]
    public void Tv_toggle_switches_the_frame_to_tv_mode()
    {
        var cut = Render<Styleguide>();
        cut.Find("button[aria-label='Vista TV']").Click();
        cut.Find(".ds-frame").ClassList.Should().Contain("tv");
    }

    [Fact]
    public void Styleguide_shows_the_map_toolbar()
    {
        var cut = Render<Styleguide>();
        cut.FindAll(".ds-map-toolbar--right").Should().HaveCount(1);
    }

    [Fact]
    public void Styleguide_shows_the_bottom_map_toolbar()
    {
        var cut = Render<Styleguide>();
        cut.FindAll(".ds-map-toolbar--bottom").Should().HaveCount(1);
    }

    [Fact]
    public void Ruler_tool_toggles_aria_pressed()
    {
        var cut = Render<Styleguide>();
        cut.Find("button[aria-label='Regla']").Click();
        cut.Find("button[aria-label='Regla']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void Add_token_shows_a_toast()
    {
        var cut = Render<Styleguide>();
        cut.Find("button[aria-label='Añadir token']").Click();
        cut.Find(".ds-toast__text").TextContent.Should().Be("2 añadidos, 3 ya estaban");
    }

    [Fact]
    public void Styleguide_shows_the_save_indicator()
    {
        var cut = Render<Styleguide>();
        cut.FindAll(".ds-save-indicator").Should().HaveCount(1);
    }
}
