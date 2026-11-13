namespace DmCompanion.Core.GameSystems;

/// <summary>Habilidad de D&amp;D 5e: clave del frontmatter, id de su característica y etiqueta.</summary>
public sealed record Dnd5eSkill(string Key, string Ability, string Label);

public static class Dnd5eSkills
{
    /// <summary>Las 18 habilidades, en el orden de la spec §9.4.</summary>
    public static IReadOnlyList<Dnd5eSkill> All { get; } =
    [
        new("acrobatics", "dex", "Acrobacias"),
        new("animal_handling", "wis", "Trato con animales"),
        new("arcana", "int", "Arcanos"),
        new("athletics", "str", "Atletismo"),
        new("deception", "cha", "Engaño"),
        new("history", "int", "Historia"),
        new("insight", "wis", "Perspicacia"),
        new("intimidation", "cha", "Intimidación"),
        new("investigation", "int", "Investigación"),
        new("medicine", "wis", "Medicina"),
        new("nature", "int", "Naturaleza"),
        new("perception", "wis", "Percepción"),
        new("performance", "cha", "Interpretación"),
        new("persuasion", "cha", "Persuasión"),
        new("religion", "int", "Religión"),
        new("sleight_of_hand", "dex", "Juego de manos"),
        new("stealth", "dex", "Sigilo"),
        new("survival", "wis", "Supervivencia"),
    ];
}
