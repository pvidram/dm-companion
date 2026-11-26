namespace DmCompanion.Core.Journal;

public sealed record JournalEntry(
    string Path, string Title, string OwnerName, string DiaryFolder, DateTimeOffset Added,
    IReadOnlyList<JournalImage> Images, string? Creature, string Note, IReadOnlyList<string> Warnings,
    bool IsSecret = false);
