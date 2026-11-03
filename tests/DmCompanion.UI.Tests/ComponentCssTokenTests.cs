using System.Text.RegularExpressions;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

/// <summary>Los componentes solo usan tokens semánticos: un tema nuevo redefine las semánticas sin tocar los CSS.</summary>
public class ComponentCssTokenTests
{
    private static readonly Regex Primitive = new(@"var\(--(wood|parchment|ink|bone|brass|blue|green|red|violet)-\d+\)");

    public static TheoryData<string> CssFiles() =>
        new(Directory.GetFiles(UiAssets.PathOf("css"), "*.css").Select(Path.GetFileName).Select(n => n!));

    [Theory]
    [MemberData(nameof(CssFiles))]
    public void Component_css_does_not_reference_primitive_tokens(string fileName)
    {
        Primitive.Matches(UiAssets.Read("css/" + fileName)).Should().BeEmpty();
    }
}
