namespace DmCompanion.Core.Glossary;

public sealed record GlossaryEntriesFile(GlossaryPackHeader Header, GlossaryCategory Category, IReadOnlyList<GlossaryEntry> Entries);
