using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Journal;

public static class JournalPaths
{
    public const string FolderName = "Diario";
    public const string LogPath = "Diario de la campaña.md";

    private static readonly string[] Supported = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic"];

    public static string DiaryFolderOf(string pcNotePath)
    {
        var path = VaultPaths.Normalize(pcNotePath);
        var directory = FolderOf(path);
        var name = VaultPaths.FileTitle(path);
        var parent = directory[(directory.LastIndexOf('/') + 1)..];
        if (directory.Length > 0 && parent.Equals(name, StringComparison.OrdinalIgnoreCase)) return $"{directory}/{FolderName}";
        return directory.Length == 0 ? $"{name}/{FolderName}" : $"{directory}/{name}/{FolderName}";
    }

    public static bool IsEntryPath(string path)
    {
        var normalized = VaultPaths.Normalize(path);
        return VaultPaths.IsIndexable(normalized) && InDiaryFolder(normalized);
    }

    public static bool IsDiaryImage(string path)
    {
        var normalized = VaultPaths.Normalize(path);
        return HasSupportedExtension(normalized) && !VaultPaths.IsExcluded(normalized)
            && !VaultPaths.IsConflictFile(normalized) && InDiaryFolder(normalized);
    }

    public static string FolderOf(string path)
    {
        var normalized = VaultPaths.Normalize(path);
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }

    public static string OwnerNameOf(string entryPath)
    {
        var segments = FolderOf(entryPath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[^2] : string.Empty;
    }

    public static string ExtensionOf(string fileName) => Path.GetExtension(fileName).ToLowerInvariant();

    public static bool HasSupportedExtension(string fileName) => Supported.Contains(ExtensionOf(fileName));

    public static bool IsDisplayable(string fileName) => HasSupportedExtension(fileName) && ExtensionOf(fileName) != ".heic";

    private static bool InDiaryFolder(string normalizedPath)
    {
        var folder = FolderOf(normalizedPath);
        return folder[(folder.LastIndexOf('/') + 1)..].Equals(FolderName, StringComparison.OrdinalIgnoreCase);
    }
}
