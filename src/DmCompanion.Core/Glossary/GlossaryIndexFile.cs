namespace DmCompanion.Core.Glossary;

public sealed record GlossaryIndexFile(GlossaryPackHeader Header, IReadOnlyList<GlossaryListing> Listings);
