using System.Net;
using System.Text;

namespace DmCompanion.Core.Glossary;

/// <summary>Único generador de HTML del glosario. Todo texto de la fuente se codifica y ningún atributo lo lleva.</summary>
public static class GlossaryHtmlRenderer
{
    public static string Render(IReadOnlyList<GlossaryBlock> blocks)
    {
        var sb = new StringBuilder();
        AppendBlocks(sb, blocks);
        return sb.ToString();
    }

    public static string Render(IReadOnlyList<GlossarySpan> spans)
    {
        var sb = new StringBuilder();
        AppendSpans(sb, spans);
        return sb.ToString();
    }

    private static void AppendBlocks(StringBuilder sb, IReadOnlyList<GlossaryBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Kind != BlockKind.ListItem) { AppendBlock(sb, blocks[i]); continue; }
            sb.Append("<ul>");
            for (; i < blocks.Count && blocks[i].Kind == BlockKind.ListItem; i++) AppendItem(sb, blocks[i]);
            i--;
            sb.Append("</ul>");
        }
    }

    private static void AppendItem(StringBuilder sb, GlossaryBlock block)
    {
        sb.Append("<li>");
        AppendSpans(sb, block.Text);
        sb.Append("</li>");
    }

    private static void AppendBlock(StringBuilder sb, GlossaryBlock block)
    {
        switch (block.Kind)
        {
            case BlockKind.Paragraph: Wrap(sb, "<p>", "</p>", block.Text); break;
            case BlockKind.Heading:
                var tag = "h" + Math.Clamp((block.Level ?? 1) + 2, 3, 6);
                Wrap(sb, $"<{tag}>", $"</{tag}>", block.Text);
                break;
            case BlockKind.Formula: Wrap(sb, "<p class=\"ds-glossary-formula\">", "</p>", block.Text); break;
            case BlockKind.Note: Wrap(sb, "<p class=\"ds-glossary-note\">", "</p>", block.Text); break;
            case BlockKind.Table: AppendTable(sb, block); break;
            case BlockKind.Fields: AppendFields(sb, block); break;
            case BlockKind.Box:
                sb.Append("<aside class=\"ds-glossary-box\">");
                if (block.Title is not null) sb.Append("<h4>").Append(WebUtility.HtmlEncode(block.Title)).Append("</h4>");
                AppendBlocks(sb, block.Children ?? []);
                sb.Append("</aside>");
                break;
            case BlockKind.Columns:
                sb.Append("<ul class=\"ds-glossary-columns\">");
                foreach (var child in block.Children ?? []) AppendItem(sb, child);
                sb.Append("</ul>");
                break;
        }
    }

    private static void AppendTable(StringBuilder sb, GlossaryBlock block)
    {
        var rows = block.Rows ?? [];
        sb.Append("<table class=\"ds-glossary-table\">");
        if (block.Title is not null) sb.Append("<caption>").Append(WebUtility.HtmlEncode(block.Title)).Append("</caption>");
        var bodyStart = 0;
        if (!block.Vertical && rows.Count > 0)
        {
            sb.Append("<thead><tr>");
            foreach (var cell in rows[0]) Wrap(sb, "<th>", "</th>", cell);
            sb.Append("</tr></thead>");
            bodyStart = 1;
        }
        sb.Append("<tbody>");
        for (var r = bodyStart; r < rows.Count; r++)
        {
            sb.Append("<tr>");
            foreach (var cell in rows[r]) Wrap(sb, "<td>", "</td>", cell);
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
    }

    private static void AppendFields(StringBuilder sb, GlossaryBlock block)
    {
        sb.Append("<dl class=\"ds-glossary-fields\">");
        foreach (var row in block.Rows ?? [])
        {
            if (row.Count < 2) continue;
            Wrap(sb, "<dt>", "</dt>", row[0]);
            Wrap(sb, "<dd>", "</dd>", row[1]);
        }
        sb.Append("</dl>");
    }

    private static void Wrap(StringBuilder sb, string open, string close, IReadOnlyList<GlossarySpan>? spans)
    {
        sb.Append(open);
        AppendSpans(sb, spans);
        sb.Append(close);
    }

    private static void AppendSpans(StringBuilder sb, IReadOnlyList<GlossarySpan>? spans)
    {
        foreach (var span in spans ?? [])
        {
            if (span.Style.HasFlag(SpanStyle.Bold)) sb.Append("<strong>");
            if (span.Style.HasFlag(SpanStyle.Italic)) sb.Append("<em>");
            sb.Append(WebUtility.HtmlEncode(span.Text));
            if (span.Style.HasFlag(SpanStyle.Italic)) sb.Append("</em>");
            if (span.Style.HasFlag(SpanStyle.Bold)) sb.Append("</strong>");
        }
    }
}
