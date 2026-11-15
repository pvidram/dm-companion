using DmCompanion.UI.Components;
using System.Text.RegularExpressions;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class SaveIndicatorTests : UiTestContext
{
    [Fact]
    public void Save_indicator_is_a_status_announced_as_saving()
    {
        var cut = Render<SaveIndicator>();
        var root = cut.Find(".ds-save-indicator");
        (root.GetAttribute("role"), root.GetAttribute("aria-label")).Should().Be(("status", "Guardando…"));
    }

    [Fact]
    public void Save_indicator_shows_the_save_icon()
    {
        var cut = Render<SaveIndicator>();
        cut.Find(".ds-save-indicator .ds-icon").GetAttribute("style").Should().Contain("icons/save.svg");
    }

    [Fact]
    public void Save_indicator_stops_blinking_with_reduced_motion()
    {
        var css = UiAssets.Read("css/content.css");
        var rule = new Regex(@"@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{\s*\.ds-save-indicator\s*\{\s*animation:\s*none");
        rule.IsMatch(css).Should().BeTrue();
    }
}
