using System.Globalization;

namespace DmCompanion.Core.Glossary;

public static class CreatureFrontmatter
{
    internal static readonly string[] AbilityIds = ["str", "dex", "con", "int", "wis", "cha"];

    /// <summary>Mismo diccionario que daría el YAML de una nota npc: todo bajo "stats", escalares como string, mapas OrdinalIgnoreCase.</summary>
    public static IReadOnlyDictionary<string, object?> From(CreatureStats stats)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ac"] = Text(stats.Ac),
            ["hp"] = Text(stats.Hp),
            ["initiative_bonus"] = Text(stats.InitiativeBonus),
            ["cr"] = stats.Cr,
            ["proficiency_bonus"] = Text(stats.ProficiencyBonus),
            ["passive_perception"] = Text(stats.PassivePerception),
        };
        if (stats.SpeedFeet is { } speed) map["speed"] = Text(speed);

        var abilities = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < AbilityIds.Length && i < stats.Abilities.Count; i++) abilities[AbilityIds[i]] = Text(stats.Abilities[i]);
        map["abilities"] = abilities;

        var saves = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, value) in DifferingSaves(stats)) saves[id] = Text(value);
        if (saves.Count > 0) map["saves"] = saves;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["stats"] = map };
    }

    /// <summary>Salvaciones que no coinciden con el modificador (las que llevan competencia).</summary>
    internal static IEnumerable<(string Id, int Value)> DifferingSaves(CreatureStats stats)
    {
        for (var i = 0; i < AbilityIds.Length && i < stats.Saves.Count && i < stats.Modifiers.Count; i++)
            if (stats.Saves[i] != stats.Modifiers[i]) yield return (AbilityIds[i], stats.Saves[i]);
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
