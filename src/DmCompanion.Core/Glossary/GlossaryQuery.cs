namespace DmCompanion.Core.Glossary;

public sealed record GlossaryFilter(
    decimal? CrMin = null, decimal? CrMax = null, string? CreatureType = null,
    int? SpellLevel = null, string? SpellClass = null, bool? Ritual = null, bool? Concentration = null,
    string? Rarity = null);

public sealed record GlossaryQuery(string? Text = null, GlossaryCategory? Category = null, GlossaryFilter? Filter = null);
