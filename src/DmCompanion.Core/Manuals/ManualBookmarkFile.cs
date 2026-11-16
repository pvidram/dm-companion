namespace DmCompanion.Core.Manuals;

public sealed record ManualBookmarkFile(int Version, IReadOnlyList<ManualBookmark> Bookmarks);
