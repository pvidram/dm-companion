namespace DmCompanion.Core.Glossary;

public sealed record CreatureStats(
    int Ac, int Hp, string HitDice, int InitiativeBonus, int InitiativeScore,
    IReadOnlyList<int> Abilities, IReadOnlyList<int> Modifiers, IReadOnlyList<int> Saves,   // orden FUE DES CON INT SAB CAR
    string Cr, int Xp, int? XpLair, int ProficiencyBonus, int PassivePerception,
    IReadOnlyList<string> SizeKeys, string TypeKey, string TypeLine,                         // TypeLine: «Aberración Grande, legal malvada»
    int? SpeedFeet,
    IReadOnlyList<GlossaryField> Fields,
    IReadOnlyList<AbilityRow> AbilityRows,
    IReadOnlyList<CreatureSection> Sections);
