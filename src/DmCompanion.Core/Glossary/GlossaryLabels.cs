using System.Globalization;

namespace DmCompanion.Core.Glossary;

/// <summary>Traduce las claves inglesas de la fuente; un valor desconocido se devuelve tal cual.</summary>
public static class GlossaryLabels
{
    private static readonly Dictionary<string, string> CreatureTypes = new()
    {
        ["Aberration"] = "Aberración", ["Beast"] = "Bestia", ["Celestial"] = "Celestial", ["Construct"] = "Autómata",
        ["Dragon"] = "Dragón", ["Elemental"] = "Elemental", ["Fey"] = "Feérico", ["Fiend"] = "Infernal",
        ["Giant"] = "Gigante", ["Humanoid"] = "Humanoide", ["Monstrosity"] = "Monstruosidad", ["Ooze"] = "Cieno",
        ["Plant"] = "Planta", ["Undead"] = "Muerto viviente",
    };

    private static readonly Dictionary<string, string> SpellClasses = new()
    {
        ["Bard"] = "Bardo", ["Cleric"] = "Clérigo", ["Druid"] = "Druida", ["Paladin"] = "Paladín",
        ["Ranger"] = "Explorador", ["Sorcerer"] = "Hechicero", ["Warlock"] = "Brujo", ["Wizard"] = "Mago",
    };

    private static readonly Dictionary<string, string> Schools = new()
    {
        ["Abjuration"] = "Abjuración", ["Conjuration"] = "Conjuración", ["Divination"] = "Adivinación",
        ["Enchantment"] = "Encantamiento", ["Evocation"] = "Evocación", ["Illusion"] = "Ilusionismo",
        ["Necromancy"] = "Nigromancia", ["Transmutation"] = "Transmutación",
    };

    private static readonly Dictionary<string, string> Rarities = new()
    {
        ["Common"] = "Común", ["Uncommon"] = "Infrecuente", ["Rare"] = "Raro", ["Very Rare"] = "Muy raro",
        ["Legendary"] = "Legendario", ["Artifact"] = "Artefacto", ["Rarity Varies"] = "Rareza variable",
    };

    public static IReadOnlyList<string> CreatureTypeKeys { get; } = [.. CreatureTypes.Keys];

    public static IReadOnlyList<string> SpellClassKeys { get; } = [.. SpellClasses.Keys];

    public static IReadOnlyList<string> RarityKeys { get; } = [.. Rarities.Keys];

    public static string CreatureType(string key) => CreatureTypes.GetValueOrDefault(key, key);

    public static string SpellClass(string key) => SpellClasses.GetValueOrDefault(key, key);

    public static string School(string key) => Schools.GetValueOrDefault(key, key);

    public static string Rarity(string key) => Rarities.GetValueOrDefault(key, key);

    public static string CategoryLabel(GlossaryCategory category) => category switch
    {
        GlossaryCategory.Rule => "Reglas",
        GlossaryCategory.Condition => "Estados",
        GlossaryCategory.Creature => "Criaturas",
        GlossaryCategory.Spell => "Conjuros",
        GlossaryCategory.MagicItem => "Objetos mágicos",
        GlossaryCategory.Equipment => "Equipo",
        GlossaryCategory.CharacterOption => "Personajes",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>«1/8», «1/4» y «1/2» a 0.125, 0.25 y 0.5; los enteros, a sí mismos.</summary>
    public static decimal CrValue(string cr) => cr switch
    {
        "1/8" => 0.125m,
        "1/4" => 0.25m,
        "1/2" => 0.5m,
        _ => decimal.TryParse(cr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0m,
    };
}
