namespace DmCompanion.Core.Glossary;

public sealed record GlossaryEntry(
    string Key, GlossaryCategory Category, string? Subtype, string Name, string? NameEn, int? Page,
    IReadOnlyList<GlossaryBlock> Blocks,
    CreatureStats? Creature = null, SpellInfo? Spell = null, ItemInfo? Item = null);
