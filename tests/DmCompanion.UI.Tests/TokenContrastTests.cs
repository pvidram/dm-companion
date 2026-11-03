using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class TokenContrastTests
{
    private const double Text = 4.5;
    private const double Graphic = 3.0;

    private static readonly IReadOnlyDictionary<string, string> Tokens = TokenSheet.Parse(UiAssets.Read("tokens.css"));

    public static TheoryData<string, string, double> Pairs()
    {
        var data = new TheoryData<string, string, double>();
        string[] sheets = ["surface-sheet", "surface-sheet-sunken", "surface-sheet-raised"];
        foreach (var sheet in sheets)
        {
            data.Add("text-ink", sheet, Text);
            data.Add("text-ink-secondary", sheet, Text);
            data.Add("text-heal", sheet, Text);
            data.Add("text-damage", sheet, Text);
            data.Add("text-dm-only", sheet, Text);
            data.Add("accent-on-sheet", sheet, Graphic);
            data.Add("dm-only", sheet, Graphic);
            data.Add("faction-outline-on-sheet", sheet, Graphic);
        }
        foreach (var wood in new[] { "surface-frame", "surface-frame-raised" })
        {
            data.Add("text-on-wood", wood, Text);
            data.Add("accent-on-wood", wood, Graphic);
            data.Add("faction-outline-on-wood", wood, Graphic);
        }
        data.Add("text-ink", "surface-dm-secret", Text);
        data.Add("text-ink-secondary", "surface-dm-secret", Text);
        data.Add("text-damage", "surface-dm-secret", Text);
        data.Add("dm-only", "surface-dm-secret", Graphic);
        data.Add("text-ink", "accent-on-wood", Text);
        data.Add("button-text-on-fill", "button-primary-bg", Text);
        data.Add("button-text-on-fill", "button-damage-bg", Text);
        data.Add("button-text-on-fill", "button-heal-bg", Text);
        data.Add("button-text-on-fill", "button-primary-hover", Text);
        data.Add("button-text-on-fill", "button-damage-hover", Text);
        data.Add("button-text-on-fill", "button-heal-hover", Text);
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Token_pair_meets_the_wcag_minimum(string foreground, string background, double minimum) =>
        WcagContrast.Ratio(Tokens[foreground], Tokens[background]).Should().BeGreaterThanOrEqualTo(minimum);

    [Fact]
    public void Contrast_of_black_on_white_is_21() =>
        WcagContrast.Ratio("#000000", "#FFFFFF").Should().BeApproximately(21, 0.01);
}
