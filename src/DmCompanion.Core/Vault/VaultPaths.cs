using System.Text.RegularExpressions;

namespace DmCompanion.Core.Vault;

public static class VaultPaths
{
    private const string MarkdownExtension = ".md";

    private static readonly Regex ConflictSuffix = new(
        @"\.sync-conflict-\d{8}-\d{6}-[A-Z0-9]{7}(?=\.[^./]+$|$)", RegexOptions.Compiled);

    public static string Normalize(string relativePath) => relativePath.Replace('\\', '/').TrimStart('/');

    public static bool IsExcluded(string relativePath) =>
        Normalize(relativePath).Split('/').Any(segment => segment.StartsWith('.'));

    public static bool IsMarkdown(string relativePath) =>
        relativePath.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsConflictFile(string relativePath) => ConflictSuffix.IsMatch(FileName(relativePath));

    public static string GetConflictOriginal(string conflictPath)
    {
        var path = Normalize(conflictPath);
        var slash = path.LastIndexOf('/');
        var original = ConflictSuffix.Replace(FileName(path), string.Empty);
        return slash < 0 ? original : path[..(slash + 1)] + original;
    }

    public static bool IsIndexable(string relativePath) =>
        IsMarkdown(relativePath) && !IsExcluded(relativePath) && !IsConflictFile(relativePath);

    public static string LinkKey(string relativePath)
    {
        var path = Normalize(relativePath);
        return IsMarkdown(path) ? path[..^MarkdownExtension.Length] : path;
    }

    public static string FileTitle(string relativePath)
    {
        var name = FileName(relativePath);
        return IsMarkdown(name) ? name[..^MarkdownExtension.Length] : name;
    }

    private static string FileName(string relativePath)
    {
        var path = Normalize(relativePath);
        return path[(path.LastIndexOf('/') + 1)..];
    }
}
