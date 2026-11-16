using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

/// <summary>An entry plus its index row, built together so they cannot drift apart.</summary>
internal sealed record MappedEntry(GlossaryEntry Entry, GlossaryListing Listing)
{
    public static MappedEntry Create(GlossaryCategory category, SourceEntry source, string? subtype, string summary,
        IReadOnlyList<GlossaryBlock> blocks, GlossaryFacets? facets = null,
        CreatureStats? creature = null, SpellInfo? spell = null, ItemInfo? item = null)
    {
        var key = GlossaryPacks.SrdKey(category, source.Id);
        var entry = new GlossaryEntry(key, category, subtype, source.Name, source.NameEn, source.Page, blocks, creature, spell, item);
        var listing = new GlossaryListing(key, category, subtype, source.Name, source.NameEn, summary, facets ?? new GlossaryFacets());
        return new MappedEntry(entry, listing);
    }
}
