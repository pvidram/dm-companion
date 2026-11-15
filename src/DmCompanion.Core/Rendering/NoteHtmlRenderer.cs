using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using Markdig;

namespace DmCompanion.Core.Rendering;

public enum RenderAudience { Dm, Players }

public static class NoteHtmlRenderer
{
    // Caracteres de uso privado: Markdig los deja intactos y nunca aparecen en notas reales.
    private const char SecretOpen = '\uE000';
    private const char SecretClose = '\uE001';
    private const char EmbedOpen = '\uE002';
    private const char EmbedClose = '\uE003';
    private const string WikilinkScheme = "wikilink:";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseEmphasisExtras().UseTaskLists().UseAutoIdentifiers().UseAutoLinks().UseFootnotes()
        .DisableHtml().Build();
    private static readonly Regex Href = new("href=\"(?<url>[^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex WikilinkAnchor = new("<a href=\"wikilink:(?<target>[^\"]*)\">", RegexOptions.Compiled);
    private static readonly Regex Src = new("src=\"(?<url>[^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex EmbedMarker = new("\uE002(?<i>\\d+)\uE003", RegexOptions.Compiled);
    // Markdig escapa '<' y '>' en el texto y en los atributos: en su HTML, un '<' literal solo puede abrir una etiqueta.
    private static readonly Regex TagOrEmbedMarker = new("(?<tag><[^>]*>?)|\uE002(?<i>\\d+)\uE003", RegexOptions.Compiled);
    private static readonly Regex CodeTag = new("^<(?<close>/?)code[\\s>]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] AllowedImageSources = ["http:", "https:", "data:image/"];
    private static readonly string[] AllowedSchemes = ["http:", "https:", "mailto:", WikilinkScheme, "#"];

    public static string RenderForPlayers(VaultEntity entity, Func<string, bool> linkExists, Func<string, string?>? imageUrl = null) =>
        entity.IsSecret ? string.Empty : Render(entity.Body, RenderAudience.Players, linkExists, imageUrl);

    /// <param name="imageUrl">Recibe el destino de un <c>![[imagen]]</c> (sin el ancho) y devuelve su URL, o null si no se resuelve.</param>
    public static string Render(string body, RenderAudience audience, Func<string, bool> linkExists, Func<string, string?>? imageUrl = null)
    {
        body = StripPrivateMarkers(body);
        var prepared = audience == RenderAudience.Players ? SecretFilter.RemoveSecretBlocksForPlayers(body) : MarkSecrets(body);
        var embeds = new List<Embed>();
        var html = Markdown.ToHtml(ReplaceWikilinks(prepared, embeds), Pipeline);
        html = Href.Replace(html, SanitizeHref);
        html = Src.Replace(html, SanitizeSrc);
        html = WikilinkAnchor.Replace(html, match => BuildAnchor(match, linkExists));
        html = RestoreEmbeds(html, embeds, imageUrl);
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

    // Un embed de imagen y su texto original, por si acaba dentro de código o de un atributo y hay que devolverlo tal cual.
    private sealed record Embed(Wikilink Link, string Source);

    private static string ReplaceWikilinks(string text, List<Embed> embeds)
    {
        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var match in WikilinkExtractor.FindMatches(text))
        {
            builder.Append(text, cursor, match.Index - cursor);
            if (match.Link.IsEmbed && MapPaths.IsImage(match.Link.Target))
            {
                embeds.Add(new Embed(match.Link, text.Substring(match.Index, match.Length)));
                builder.Append(EmbedOpen).Append(embeds.Count - 1).Append(EmbedClose);
                cursor = match.Index + match.Length;
                continue;
            }
            builder.Append('[').Append(EscapeLinkText(match.Link.DisplayText)).Append("](")
                .Append(WikilinkScheme).Append(Uri.EscapeDataString(match.Link.Target)).Append(')');
            cursor = match.Index + match.Length;
        }
        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }

    // El <img> se inserta tras el saneado de src: la URL la pone el resolvedor de la app, no el texto de la nota.
    private static string BuildImage(Wikilink link, Func<string, string?>? imageUrl)
    {
        var name = EncodeAttribute(link.Target);
        if (imageUrl?.Invoke(link.Target) is not { } url) return $"<span class=\"wikilink broken\">{name}</span>";
        var width = int.TryParse(link.Alias, NumberStyles.None, CultureInfo.InvariantCulture, out var w) && w > 0 ? $" width=\"{w}\"" : "";
        return $"<img src=\"{EncodeAttribute(url)}\" alt=\"{name}\"{width}>";
    }

    // El texto de la nota no puede traer los marcadores internos: falsificarían secretos o embeds.
    private static string StripPrivateMarkers(string body) =>
        body.IndexOfAny([SecretOpen, SecretClose, EmbedOpen, EmbedClose]) < 0
            ? body
            : body.Replace(SecretOpen.ToString(), "").Replace(SecretClose.ToString(), "")
                .Replace(EmbedOpen.ToString(), "").Replace(EmbedClose.ToString(), "");

    // Solo un marcador en el texto normal se pinta como imagen (y solo entonces se resuelve su URL: en la TV, resolverla la mete
    // en la lista blanca). Dentro de <code> (Markdig decide qué es código: bloques sangrados, spans de varias líneas…) o de un
    // atributo (título de enlace, alt) vuelve a ser el texto original, escapado.
    private static string RestoreEmbeds(string html, List<Embed> embeds, Func<string, string?>? imageUrl)
    {
        if (embeds.Count == 0) return html;
        var codeDepth = 0;
        return TagOrEmbedMarker.Replace(html, match =>
        {
            if (match.Groups["tag"].Success)
            {
                var tag = match.Value;
                if (CodeTag.Match(tag) is { Success: true } code)
                    codeDepth = code.Groups["close"].Length > 0 ? Math.Max(0, codeDepth - 1) : codeDepth + 1;
                return EmbedMarker.Replace(tag, marker => EmbedAt(marker, embeds) is { } embed ? EncodeAttribute(embed.Source) : marker.Value);
            }
            if (EmbedAt(match, embeds) is not { } found) return match.Value;
            return codeDepth > 0 ? EncodeAttribute(found.Source) : BuildImage(found.Link, imageUrl);
        });
    }

    private static Embed? EmbedAt(Match marker, List<Embed> embeds) =>
        int.TryParse(marker.Groups["i"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < embeds.Count
            ? embeds[index]
            : null;

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
