using System.Globalization;
using System.Text.RegularExpressions;

namespace DmCompanion.Screens.Services;

/// <summary>Versiones SemVer 2.0 (MAJOR.MINOR.PATCH[-prerelease]) de las releases; se ignoran los metadatos «+build».</summary>
public static partial class ReleaseVersion
{
    [GeneratedRegex(@"^([0-9]+)\.([0-9]+)\.([0-9]+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex Pattern();

    public static string Normalize(string tag)
    {
        var trimmed = tag.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V') ? trimmed[1..] : trimmed;
    }

    /// <summary>Negativo si a &lt; b, 0 si iguales, positivo si a &gt; b; null si alguna no es SemVer.</summary>
    public static int? Compare(string a, string b)
    {
        var left = Pattern().Match(Normalize(a));
        var right = Pattern().Match(Normalize(b));
        if (!left.Success || !right.Success) return null;
        // Se leen todos los números antes de comparar: una versión con un número ilegible es ilegible entera.
        var leftNumbers = new long[3];
        var rightNumbers = new long[3];
        for (var i = 0; i < 3; i++)
        {
            if (!TryParseNumber(left.Groups[i + 1].Value, out leftNumbers[i])
                || !TryParseNumber(right.Groups[i + 1].Value, out rightNumbers[i])) return null;
        }
        for (var i = 0; i < 3; i++)
        {
            var byNumber = leftNumbers[i].CompareTo(rightNumbers[i]);
            if (byNumber != 0) return byNumber;
        }
        return ComparePrerelease(left.Groups[4].Value, right.Groups[4].Value);
    }

    // Solo dígitos ASCII; un número que no cabe en long cuenta como ilegible.
    private static bool TryParseNumber(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    // Sin prerrelease es mayor que con ella; los identificadores numéricos se comparan como números (SemVer §11).
    private static int ComparePrerelease(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return b.Length.CompareTo(a.Length);
        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftIsNumber = TryParseNumber(left[i], out var leftNumber);
            var rightIsNumber = TryParseNumber(right[i], out var rightNumber);
            var result = (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i]),
            };
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
}
