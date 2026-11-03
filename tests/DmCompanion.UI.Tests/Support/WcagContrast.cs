using System.Globalization;

namespace DmCompanion.UI.Tests.Support;

internal static class WcagContrast
{
    public static double Ratio(string hexA, string hexB)
    {
        var (lighter, darker) = Order(Luminance(hexA), Luminance(hexB));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static (double, double) Order(double a, double b) => a >= b ? (a, b) : (b, a);

    private static double Luminance(string hex)
    {
        var digits = hex.TrimStart('#');
        double Channel(int offset)
        {
            var value = int.Parse(digits.AsSpan(offset, 2), NumberStyles.HexNumber) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }
}
