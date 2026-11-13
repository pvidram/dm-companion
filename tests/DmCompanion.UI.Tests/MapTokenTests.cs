using System.Text.RegularExpressions;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

/// <summary>Konva lee estos tokens con getComputedStyle: si uno desaparece, el mapa se pinta sin color ni grosor.</summary>
public class MapTokenTests
{
    public static TheoryData<string> KonvaTokens() => new(
    [
        "map-grid-line-dark", "map-grid-line-light", "map-grid-width-dark", "map-grid-width-light", "map-grid-opacity",
        "map-measure-line-dark", "map-measure-line-light", "map-measure-width-dark", "map-measure-width-light",
        "map-measure-label-bg", "map-measure-label-text",
        "map-calibration-line-dark", "map-calibration-line-light", "map-calibration-width-dark", "map-calibration-width-light",
        "map-calibration-dash",
        "map-token-hidden-opacity", "map-hidden-badge-bg", "map-hidden-badge-icon",
        "z-toolbar", "z-toast",
    ]);

    [Theory]
    [MemberData(nameof(KonvaTokens))]
    public void Tokens_css_declares_the_map_token(string name) =>
        Regex.IsMatch(UiAssets.Read("tokens.css"), @"--" + Regex.Escape(name) + @"\s*:").Should().BeTrue();
}
