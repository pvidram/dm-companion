using System.Globalization;
using System.Text;

namespace DmCompanion.Core.Tv;

/// <summary>Código fijo de la dirección de la TV (<c>/tv/{código}/</c>): formato, normalización y propuesta por defecto.</summary>
public static class TvCode
{
    public const int MinLength = 4;
    public const int MaxLength = 32;
    private const string Fallback = "mesa";

    /// <summary>De 4 a 32 caracteres de <c>[a-z0-9-]</c>, sin empezar ni acabar en guion; se valida tras pasar a minúsculas.</summary>
    public static bool IsValid(string? code)
    {
        if (code is null) return false;
        var value = Normalize(code);
        return value.Length is >= MinLength and <= MaxLength
            && value[0] != '-' && value[^1] != '-'
            && value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }

    /// <summary>Minúsculas y sin espacios alrededor.</summary>
    public static string Normalize(string code) => code.Trim().ToLowerInvariant();

    /// <summary>Nombre de la campaña convertido al formato del código; <c>mesa</c> si no queda nada válido.</summary>
    public static string Suggest(string? campaignName)
    {
        var builder = new StringBuilder();
        foreach (var c in RemoveAccents(campaignName ?? string.Empty).ToLowerInvariant())
        {
            var keep = c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
            if (keep) builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        var text = builder.ToString().Trim('-');
        if (text.Length > MaxLength) text = text[..MaxLength].TrimEnd('-');
        return IsValid(text) ? text : Fallback;
    }

    private static string RemoveAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        }
        return builder.ToString();
    }
}
