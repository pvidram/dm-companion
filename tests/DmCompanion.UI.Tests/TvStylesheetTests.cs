using System.Text.RegularExpressions;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

/// <summary>Doble seguro: aunque Core ya filtra los secretos, el CSS de la TV oculta cualquier marca de DM.</summary>
public class TvStylesheetTests
{
    [Theory]
    [InlineData("dm-only")]
    [InlineData("dm-secret")]
    public void Tv_stylesheet_hides_dm_content(string cssClass)
    {
        var css = UiAssets.Read("css/tv.css");
        var rule = new Regex(@"\.tv\s+\." + cssClass + @"\b[^{]*\{[^}]*display:\s*none\s*!important");
        rule.IsMatch(css).Should().BeTrue();
    }

    [Fact]
    public void Tv_stylesheet_is_the_last_import_of_ui_css()
    {
        var imports = Regex.Matches(UiAssets.Read("ui.css"), @"@import\s+url\(""([^""]+)""\)");
        imports[^1].Groups[1].Value.Should().Be("css/tv.css");
    }
}
