using DmCompanion.UI.Pages;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class StyleguideTests : UiTestContext
{
    [Fact]
    public void Styleguide_renders_one_parchment_per_sample()
    {
        var cut = Render<Styleguide>();
        cut.FindAll(".ds-parchment").Should().HaveCount(4);
    }

    [Fact]
    public void Tv_toggle_switches_the_frame_to_tv_mode()
    {
        var cut = Render<Styleguide>();
        cut.Find("button[aria-label='Vista TV']").Click();
        cut.Find(".ds-frame").ClassList.Should().Contain("tv");
    }
}
