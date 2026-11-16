using System.Text.Json;

namespace DmCompanion.Core.Manuals;

public static class ManualBookmarks
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>null → (null,false); JSON inválido o versión mayor que la actual → (null,true).</summary>
    public static BookmarksRead Parse(string? json)
    {
        if (json is null) return new BookmarksRead(null, false);
        try
        {
            var file = JsonSerializer.Deserialize<ManualBookmarkFile>(json, Options);
            if (file is null || file.Version < 1 || file.Version > CurrentVersion || !AreValid(file.Bookmarks))
                return new BookmarksRead(null, true);
            return new BookmarksRead(file, false);
        }
        catch (JsonException)
        {
            return new BookmarksRead(null, true);
        }
    }

    private static bool AreValid(IReadOnlyList<ManualBookmark>? bookmarks) =>
        bookmarks is not null
        && bookmarks.All(b => b is not null && !string.IsNullOrWhiteSpace(b.Id)
            && !string.IsNullOrWhiteSpace(b.Name) && b.Page >= 1);

    public static string Serialize(ManualBookmarkFile file) => JsonSerializer.Serialize(file, Options) + "\n";

    /// <summary>Fusión tras un cambio externo: la base es "disk"; se añaden los de "mine" cuyo Id no esté; los borrados en "mine" respecto a "original" se quitan.</summary>
    public static ManualBookmarkFile Merge(ManualBookmarkFile original, ManualBookmarkFile mine, ManualBookmarkFile disk)
    {
        var mineIds = mine.Bookmarks.Select(b => b.Id).ToHashSet();
        var deleted = original.Bookmarks.Select(b => b.Id).Where(id => !mineIds.Contains(id)).ToHashSet();
        var result = disk.Bookmarks.Where(b => !deleted.Contains(b.Id)).ToList();
        var present = result.Select(b => b.Id).ToHashSet();
        result.AddRange(mine.Bookmarks.Where(b => !present.Contains(b.Id)));
        return new ManualBookmarkFile(CurrentVersion, result);
    }
}
