using System.Globalization;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.GameSystems;

public static class ProficiencyBonus
{
    public const string MissingReason = "Falta el bonificador de competencia (level, cr o proficiency_bonus).";

    private static readonly string[] FractionalCr = ["1/8", "1/4", "1/2"];

    /// <param name="stats">Mapa «stats» del frontmatter.</param>
    public static (int? Value, string? Reason) Resolve(IReadOnlyDictionary<string, object?> stats)
    {
        if (FrontmatterReader.GetInt(stats, "proficiency_bonus") is { } explicitBonus) return (explicitBonus, null);
        if (FrontmatterReader.GetInt(stats, "level") is >= 1 and var level) return (2 + (level - 1) / 4, null);
        if (ParseChallengeRating(FrontmatterReader.GetString(stats, "cr")) is { } cr) return (FromChallengeRating(cr), null);
        return (null, MissingReason);
    }

    private static int? ParseChallengeRating(string? text)
    {
        if (text is null) return null;
        if (FractionalCr.Contains(text)) return 0;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cr) && cr is >= 0 and <= 30 ? cr : null;
    }

    private static int FromChallengeRating(int cr) => cr switch
    {
        <= 4 => 2,
        <= 8 => 3,
        <= 12 => 4,
        <= 16 => 5,
        <= 20 => 6,
        <= 24 => 7,
        <= 28 => 8,
        _ => 9,
    };
}
