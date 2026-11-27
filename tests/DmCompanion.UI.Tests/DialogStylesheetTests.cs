using System.Text.RegularExpressions;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

/// <summary>Un diálogo más alto que la ventana no debe esconder su pie: el alto se limita a la ventana y el cuerpo hace scroll.</summary>
public class DialogStylesheetTests
{
    private static bool RuleHas(string selector, string declaration)
    {
        var css = UiAssets.Read("css/overlays.css");
        return new Regex(selector + @"\s*\{[^}]*" + declaration).IsMatch(css);
    }

    [Fact]
    public void Dialog_backdrop_row_is_bounded_by_the_viewport()
    {
        RuleHas(@"\.ds-dialog-backdrop", @"grid-template-rows:\s*minmax\(0,\s*1fr\)").Should().BeTrue();
    }

    [Theory]
    [InlineData(@"flex:\s*1\s*;")]
    [InlineData(@"min-height:\s*0\s*;")]
    public void Dialog_body_shrinks_to_scroll(string declaration)
    {
        RuleHas(@"\.ds-dialog__body", declaration).Should().BeTrue();
    }
}
