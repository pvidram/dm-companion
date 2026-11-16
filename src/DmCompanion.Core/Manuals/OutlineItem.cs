namespace DmCompanion.Core.Manuals;

public sealed record OutlineItem(string Title, int? Page, IReadOnlyList<OutlineItem> Children);
