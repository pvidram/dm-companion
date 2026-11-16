using System.Globalization;
using System.Text;

namespace DmCompanion.Core.Glossary;

public static class GlossaryText
{
    /// <summary>Minúsculas invariantes y sin diacríticos (FormD sin NonSpacingMark): «Ñandú» → «nandu».</summary>
    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
