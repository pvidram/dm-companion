using System.Text;
using System.Text.RegularExpressions;
using DmCompanion.Core.Vault;
using Markdig;

namespace DmCompanion.Core.Rendering;

public enum RenderAudience { Dm, Players }

public static class NoteHtmlRenderer
{
    // Caracteres de uso privado: Markdig los deja intactos y nunca aparecen en notas reales.
    private const char SecretOpen = '\uE000';
    private const char SecretClose = '\uE001';
    private const string WikilinkScheme = "wikilink:";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseEmphasisExtras().UseTaskLists().UseAutoIdentifiers().UseAutoLinks().UseFootnotes()
        .DisableHtml().Build();
    private static readonly Regex Href = new("href=\"(?<url>[^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex WikilinkAnchor = new("<a href=\"wikilink:(?<target>[^\"]*)\">", RegexOptions.Compiled);
    private static readonly Regex Src = new("src=\"(?<url>[^\"]*)\"", RegexOptions.Compiled);
    private static readonly string[] AllowedImageSources = ["http:", "https:", "data:image/"];
    private static readonly string[] AllowedSchemes = ["http:", "https:", "mailto:", WikilinkScheme, "#"];

    public static string RenderForPlayers(VaultEntity entity, Func<string, bool> linkExists) =>
        entity.IsSecret ? string.Empty : Render(entity.Body, RenderAudience.Players, linkExists);

    public static string Render(string body, RenderAudience audience, Func<string, bool> linkExists)
    {
        var prepared = audience == RenderAudience.Players ? SecretFilter.RemoveSecretBlocksForPlayers(body) : MarkSecrets(body);
        var html = Markdown.ToHtml(ReplaceWikilinks(prepared), Pipeline);
        html = Href.Replace(html, SanitizeHref);
        html = Src.Replace(html, SanitizeSrc);
        html = WikilinkAnchor.Replace(html, match => BuildAnchor(match, linkExists));
        return html
            .Replace(SecretOpen.ToString(), "<span class=\"dm-secret\">")
            .Replace(SecretClose.ToString(), "</span>");
    }

    private static string MarkSecrets(string body)
    {
        var builder = new StringBuilder(body.Length);
        var cursor = 0;
        foreach (var range in SecretFilter.FindSecretRanges(body))
        {
            builder.Append(body, cursor, range.Start - cursor);
            var end = range.Start + range.Length;
            var closed = range.Length >= 4 && body.AsSpan(end - 2, 2) is "%%";
            var innerStart = range.Start + 2;
            var innerEnd = closed ? end - 2 : end;
            AppendSecret(builder, body.Substring(innerStart, innerEnd - innerStart));
            cursor = end;
        }
        builder.Append(body, cursor, body.Length - cursor);
        return builder.ToString();
    }

    // Cada línea no vacía lleva su propio par: así el HTML queda equilibrado aunque el secreto abarque varios párrafos.
    private static void AppendSecret(StringBuilder builder, string inner)
    {
        var first = true;
        var lines = inner.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var content = line.TrimEnd('\r');
            if (content.Trim().Length > 0)
            {
                builder.Append(SecretOpen).Append(first ? "🔒 " : "").Append(content).Append(SecretClose).Append(line, content.Length, line.Length - content.Length);
                first = false;
            }
            else builder.Append(line);
            if (i < lines.Length - 1) builder.Append('\n');
        }
    }

    private static string ReplaceWikilinks(string text)
    {
        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var match in WikilinkExtractor.FindMatches(text))
        {
            builder.Append(text, cursor, match.Index - cursor);
            builder.Append('[').Append(EscapeLinkText(match.Link.DisplayText)).Append("](")
                .Append(WikilinkScheme).Append(Uri.EscapeDataString(match.Link.Target)).Append(')');
            cursor = match.Index + match.Length;
        }
        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }

    private static string EscapeLinkText(string text) =>
        text.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");

    private static string SanitizeHref(Match match) =>
        AllowedSchemes.Any(scheme => match.Groups["url"].Value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            ? match.Value
            : "href=\"#\"";

    private static string SanitizeSrc(Match match) =>
        AllowedImageSources.Any(scheme => match.Groups["url"].Value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            ? match.Value
            : "src=\"\"";

    private static string BuildAnchor(Match match, Func<string, bool> linkExists)
    {
        var target = Uri.UnescapeDataString(match.Groups["target"].Value);
        var css = linkExists(target) ? "wikilink" : "wikilink broken";
        return $"<a href=\"#\" class=\"{css}\" data-target=\"{EncodeAttribute(target)}\">";
    }

    // WebUtility.HtmlEncode convierte las tildes en entidades (&#243;); basta con escapar lo peligroso.
    private static string EncodeAttribute(string value) =>
        value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
}
