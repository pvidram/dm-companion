namespace DmCompanion.Core.Vault;

public enum Ability { Str, Dex, Con, Int, Wis, Cha }

public sealed record CharacterStats(
    int? ArmorClass,
    int? HitPoints,
    int? Speed,
    int? InitiativeBonus,
    IReadOnlyDictionary<Ability, int> AbilityScores)
{
    public static CharacterStats FromFrontmatter(IReadOnlyDictionary<string, object?> frontmatter)
    {
        var stats = FrontmatterReader.GetMap(frontmatter, "stats");
        var abilities = FrontmatterReader.GetMap(stats, "abilities");
        var scores = new Dictionary<Ability, int>();
        foreach (var ability in Enum.GetValues<Ability>())
        {
            if (FrontmatterReader.GetInt(abilities, ability.ToString().ToLowerInvariant()) is { } score)
                scores[ability] = score;
        }

        return new CharacterStats(
            FrontmatterReader.GetInt(stats, "ac"),
            FrontmatterReader.GetInt(stats, "hp"),
            FrontmatterReader.GetInt(stats, "speed"),
            FrontmatterReader.GetInt(stats, "initiative_bonus"),
            scores);
    }

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);
}
