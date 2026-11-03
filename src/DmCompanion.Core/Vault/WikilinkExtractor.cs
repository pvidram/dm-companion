using System.Text.RegularExpressions;

namespace DmCompanion.Core.Vault;

public sealed record Wikilink(string Target, string? Section, string? Alias, bool IsEmbed)
{
    public string DisplayText => Alias ?? (Section is null ? Target : $"{Target} › {Section}");
}

public sealed record WikilinkMatch(Wikilink Link, int Index, int Length);

public static class WikilinkExtractor
{
    private static readonly Regex Pattern = new(@"(?<embed>!?)\[\[(?<inner>[^\[\]\r\n]+?)\]\]", RegexOptions.Compiled);

    public static IReadOnlyList<Wikilink> Extract(string body) =>
        FindMatches(body).Select(match => match.Link).ToList();

    public static IReadOnlyList<WikilinkMatch> FindMatches(string body)
    {
        var masked = MarkdownCodeMasker.Mask(body);
        var matches = new List<WikilinkMatch>();
        foreach (Match match in Pattern.Matches(masked))
        {
            var link = ParseInner(match.Groups["inner"].Value, match.Groups["embed"].Value == "!");
            if (link is not null) matches.Add(new WikilinkMatch(link, match.Index, match.Length));
        }
        return matches;
    }

    private static Wikilink? ParseInner(string inner, bool isEmbed)
    {
        var pipe = inner.IndexOf('|');
        var reference = (pipe >= 0 ? inner[..pipe] : inner).TrimEnd('\\');
        var alias = pipe >= 0 ? NullIfBlank(inner[(pipe + 1)..]) : null;
        var hash = reference.IndexOf('#');
        var target = (hash >= 0 ? reference[..hash] : reference).Trim();
        var section = hash >= 0 ? NullIfBlank(reference[(hash + 1)..]) : null;
        return target.Length == 0 ? null : new Wikilink(target, section, alias, isEmbed);
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
