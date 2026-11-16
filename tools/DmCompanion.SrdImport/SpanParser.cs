using System.Net;
using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

/// <summary>Turns the source's inline HTML (b, strong, i and entities only) into styled spans.</summary>
internal static class SpanParser
{
    public static IReadOnlyList<GlossarySpan> Parse(string html, string entryId)
    {
        var spans = new List<GlossarySpan>();
        var style = SpanStyle.None;
        var bold = 0;
        var italic = 0;
        var position = 0;

        while (position < html.Length)
        {
            var open = html.IndexOf('<', position);
            if (open < 0)
            {
                Add(spans, html[position..], style);
                break;
            }

            Add(spans, html[position..open], style);
            var close = html.IndexOf('>', open);
            if (close < 0) throw new FormatException($"{entryId}: etiqueta HTML sin cerrar");

            var tag = html[(open + 1)..close].Trim().ToLowerInvariant();
            switch (tag)
            {
                case "b" or "strong": bold++; break;
                case "i": italic++; break;
                case "/b" or "/strong" when bold > 0: bold--; break;
                case "/i" when italic > 0: italic--; break;
                default: throw new FormatException($"{entryId}: etiqueta HTML no admitida <{tag}>");
            }

            style = (bold > 0 ? SpanStyle.Bold : SpanStyle.None) | (italic > 0 ? SpanStyle.Italic : SpanStyle.None);
            position = close + 1;
        }

        if (bold != 0 || italic != 0) throw new FormatException($"{entryId}: etiquetas HTML sin cerrar");
        return spans;
    }

    /// <summary>Joins spans with the same style so the output stays stable and small.</summary>
    private static void Add(List<GlossarySpan> spans, string raw, SpanStyle style)
    {
        if (raw.Length == 0) return;
        var text = WebUtility.HtmlDecode(raw);
        if (spans.Count > 0 && spans[^1].Style == style) spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
        else spans.Add(new GlossarySpan(text, style));
    }
}
