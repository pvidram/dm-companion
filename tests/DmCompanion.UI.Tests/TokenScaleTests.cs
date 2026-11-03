using System.Text.RegularExpressions;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

/// <summary>Los tokens derivados de --ui-scale deben recalcularse donde cambia la escala (.tv), no solo en :root.</summary>
public class TokenScaleTests
{
    [Theory]
    [InlineData("--font-size-body")]
    [InlineData("--space-4")]
    public void Scaled_tokens_are_declared_in_a_rule_that_also_matches_ds_root(string token)
    {
        var css = Regex.Replace(UiAssets.Read("tokens.css"), @"/\*.*?\*/", "", RegexOptions.Singleline);
        var rule = new Regex(@"(?<selector>[^{}]+)\{[^}]*" + Regex.Escape(token) + @"\s*:");
        rule.Match(css).Groups["selector"].Value.Should().Contain(".ds-root");
    }
}
