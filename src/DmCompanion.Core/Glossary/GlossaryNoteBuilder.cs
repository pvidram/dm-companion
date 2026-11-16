using System.Globalization;
using System.Text;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Glossary;

public sealed record GlossaryNote(EntityType Type, string Title, string RelativePath, string Content);

/// <summary>Construye la nota Markdown de «Copiar al vault». Frontmatter como texto, igual que NoteTemplates.</summary>
public static class GlossaryNoteBuilder
{
    public static GlossaryNote Build(GlossaryEntry entry)
    {
        var type = entry.Creature is null ? EntityType.Rule : EntityType.Monster;
        var sb = new StringBuilder();
        sb.Append("---\ntype: ").Append(EntityTypes.ToKey(type)).Append("\nname: ").Append(Quote(entry.Name)).Append('\n');
        if (entry.Creature is { } creature) AppendStats(sb, creature);
        sb.Append("source: ").Append(Quote(entry.Key)).Append("\n---\n\n");
        AppendBody(sb, entry);
        var path = $"{NoteTemplates.DefaultFolder(type)}/{NoteFileName.FromTitle(entry.Name)}.md";
        return new GlossaryNote(type, entry.Name, path, sb.ToString());
    }

    private static void AppendStats(StringBuilder sb, CreatureStats c)
    {
        sb.Append("stats:\n");
        sb.Append("  ac: ").Append(Num(c.Ac)).Append("\n  hp: ").Append(Num(c.Hp)).Append('\n');
        if (c.SpeedFeet is { } speed) sb.Append("  speed: ").Append(Num(speed)).Append('\n');
        sb.Append("  initiative_bonus: ").Append(Num(c.InitiativeBonus)).Append('\n');
        sb.Append("  cr: ").Append(Quote(c.Cr)).Append('\n');
        sb.Append("  proficiency_bonus: ").Append(Num(c.ProficiencyBonus)).Append('\n');
        sb.Append("  passive_perception: ").Append(Num(c.PassivePerception)).Append('\n');
        var abilities = CreatureFrontmatter.AbilityIds.Take(c.Abilities.Count).Select((id, i) => $"{id}: {Num(c.Abilities[i])}");
        sb.Append("  abilities: { ").Append(string.Join(", ", abilities)).Append(" }\n");
        var saves = CreatureFrontmatter.DifferingSaves(c).Select(s => $"{s.Id}: {Num(s.Value)}").ToList();
        if (saves.Count > 0) sb.Append("  saves: { ").Append(string.Join(", ", saves)).Append(" }\n");
    }

    private static void AppendBody(StringBuilder sb, GlossaryEntry entry)
    {
        if (entry.Creature is { } c) AppendCreature(sb, c);
        AppendBlocks(sb, entry.Blocks);
        sb.Append(entry.Page is { } page
            ? $"*SRD 5.2.1, p. {Num(page)}. CC-BY 4.0, Wizards of the Coast.*\n"
            : "*SRD 5.2.1. CC-BY 4.0, Wizards of the Coast.*\n");
    }

    private static void AppendCreature(StringBuilder sb, CreatureStats c)
    {
        sb.Append(Spans([new GlossarySpan(c.TypeLine, SpanStyle.Italic)])).Append("\n\n");
        foreach (var field in c.Fields) sb.Append("**").Append(Escape(field.Label)).Append(":** ").Append(Spans(field.Value)).Append("\n\n");
        if (c.AbilityRows.Count > 0)
        {
            sb.Append("| Característica | Puntuación | Mod. | Salvación |\n| --- | --- | --- | --- |\n");
            foreach (var row in c.AbilityRows)
                sb.Append($"| {Cell(row.Label)} | {Cell(row.Score)} | {Cell(row.Modifier)} | {Cell(row.Save)} |\n");
            sb.Append('\n');
        }
        foreach (var section in c.Sections)
        {
            sb.Append("## ").Append(Escape(section.Title)).Append("\n\n");
            if (section.Intro is { Count: > 0 }) sb.Append(Spans(section.Intro)).Append("\n\n");
            foreach (var item in section.Items) AppendFeature(sb, item);
        }
    }

    private static void AppendFeature(StringBuilder sb, CreatureFeature item)
    {
        sb.Append("***").Append(Escape(item.Name)).Append(".***");
        var rest = item.Blocks;
        if (rest.Count > 0 && rest[0].Kind == BlockKind.Paragraph)
        {
            sb.Append(' ').Append(Spans(rest[0].Text)).Append("\n\n");
            rest = rest.Skip(1).ToList();
        }
        else sb.Append("\n\n");
        AppendBlocks(sb, rest);
    }

    private static void AppendBlocks(StringBuilder sb, IReadOnlyList<GlossaryBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            AppendBlock(sb, block);
            var endsList = block.Kind is BlockKind.ListItem or BlockKind.Columns
                && !(i + 1 < blocks.Count && blocks[i + 1].Kind == BlockKind.ListItem && block.Kind == BlockKind.ListItem);
            if (block.Kind is not BlockKind.ListItem || endsList) sb.Append('\n');
        }
    }

    private static void AppendBlock(StringBuilder sb, GlossaryBlock block)
    {
        switch (block.Kind)
        {
            case BlockKind.Heading:
                sb.Append(new string('#', Math.Clamp((block.Level ?? 1) + 1, 2, 6))).Append(' ').Append(Spans(block.Text)).Append('\n');
                break;
            case BlockKind.ListItem: sb.Append("- ").Append(Spans(block.Text)).Append('\n'); break;
            case BlockKind.Columns:
                foreach (var child in block.Children ?? []) sb.Append("- ").Append(Spans(child.Text)).Append('\n');
                break;
            case BlockKind.Table: AppendTable(sb, block); break;
            case BlockKind.Fields:
                foreach (var row in block.Rows ?? [])
                    if (row.Count >= 2) sb.Append("**").Append(Spans(row[0])).Append(":** ").Append(Spans(row[1])).Append("\n\n");
                if (block.Rows is { Count: > 0 }) sb.Length -= 1;
                break;
            case BlockKind.Box: AppendBox(sb, block); break;
            default: sb.Append(Spans(block.Text)).Append('\n'); break;
        }
    }

    private static void AppendBox(StringBuilder sb, GlossaryBlock block)
    {
        var inner = new StringBuilder();
        if (block.Title is not null) inner.Append("**").Append(Escape(block.Title)).Append("**\n\n");
        AppendBlocks(inner, block.Children ?? []);
        foreach (var line in inner.ToString().TrimEnd('\n').Split('\n'))
            sb.Append(line.Length == 0 ? ">" : "> " + line).Append('\n');
    }

    private static void AppendTable(StringBuilder sb, GlossaryBlock block)
    {
        var rows = block.Rows ?? [];
        if (rows.Count == 0) return;
        if (block.Title is not null) sb.Append("**").Append(Escape(block.Title)).Append("**\n\n");
        var width = rows.Max(r => r.Count);
        var start = 0;
        if (block.Vertical) sb.Append('|').Append(string.Concat(Enumerable.Repeat("  |", width))).Append('\n');
        else { AppendRow(sb, rows[0], width); start = 1; }
        sb.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", width))).Append('\n');
        for (var r = start; r < rows.Count; r++) AppendRow(sb, rows[r], width);
    }

    private static void AppendRow(StringBuilder sb, IReadOnlyList<IReadOnlyList<GlossarySpan>> row, int width)
    {
        sb.Append('|');
        for (var c = 0; c < width; c++) sb.Append(' ').Append(c < row.Count ? Cell(Spans(row[c])) : string.Empty).Append(" |");
        sb.Append('\n');
    }

    private static string Cell(string markdown) => markdown.Replace("|", "\\|").Replace("\n", " ");

    private static string Spans(IReadOnlyList<GlossarySpan>? spans)
    {
        var sb = new StringBuilder();
        foreach (var span in spans ?? [])
        {
            var text = Escape(span.Text);
            var core = text.Trim();
            var mark = (span.Style.HasFlag(SpanStyle.Bold) ? "**" : "") + (span.Style.HasFlag(SpanStyle.Italic) ? "*" : "");
            if (mark.Length == 0 || core.Length == 0) { sb.Append(text); continue; }
            // Los marcadores pegados a un espacio no formatean en Markdown: el espacio va fuera.
            var lead = text[..(text.Length - text.TrimStart().Length)];
            var trail = text[text.TrimEnd().Length..];
            var closing = new string(mark.Reverse().ToArray());
            sb.Append(lead).Append(mark).Append(core).Append(closing).Append(trail);
        }
        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("[[", "\\[\\[");

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Réplica del escape mínimo de NoteTemplates.Quote (privado): no se hace público solo por esto.
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
