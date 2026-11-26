using System.Globalization;
using System.Text.RegularExpressions;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Journal;

public static partial class JournalNames
{
    public const string DefaultTitle = "Entrada";
    private const int MaxTitleLength = 100;
    private static readonly char[] Invalid = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '^', '[', ']'];

    public static string SafeTitle(string? title)
    {
        var replaced = new string((title ?? string.Empty).Select(c => Invalid.Contains(c) || char.IsControl(c) ? '-' : c).ToArray());
        var text = Spaces().Replace(replaced, " ").Trim().TrimEnd('.', ' ');
        if (text.Length > MaxTitleLength) text = text[..MaxTitleLength].TrimEnd('.', ' ');
        if (text.Length == 0) return DefaultTitle;
        return NoteFileName.IsReserved(text) ? text + "_" : text;
    }

    public static string DatePrefix(DateTimeOffset added) => added.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string BaseName(DateTimeOffset added, string? title) => $"{DatePrefix(added)} {SafeTitle(title)}";

    public static string Unique(string baseName, Func<string, bool> taken)
    {
        if (!taken(baseName)) return baseName;
        for (var number = 2; ; number++)
        {
            var candidate = $"{baseName} {number}";
            if (!taken(candidate)) return candidate;
        }
    }

    public static string ImageName(string baseName, int number, string extension) =>
        $"{baseName}-{number.ToString(CultureInfo.InvariantCulture)}{extension.ToLowerInvariant()}";

    public static string TitleOf(string baseName)
    {
        var match = DatedName().Match(baseName);
        if (!match.Success) return baseName;
        var title = match.Groups["title"].Value.Trim();
        return title.Length == 0 ? DefaultTitle : title;
    }

    public static string RenamedBase(string currentBase, DateTimeOffset added, string? newTitle)
    {
        var match = DatedName().Match(currentBase);
        var prefix = match.Success ? match.Groups["date"].Value : DatePrefix(added);
        return $"{prefix} {SafeTitle(newTitle)}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2}) (?<title>.*)$")]
    private static partial Regex DatedName();
}
