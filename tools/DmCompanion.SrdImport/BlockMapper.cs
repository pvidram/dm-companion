using System.Text.Json;
using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

internal static class BlockMapper
{
    public static IReadOnlyList<GlossaryBlock> MapAll(JsonElement blocks, string id)
    {
        if (blocks.ValueKind != JsonValueKind.Array) return [];
        return blocks.EnumerateArray().Select(b => Map(b, id)).ToList();
    }

    public static IReadOnlyList<GlossarySpan> Spans(string? text, string id) => SpanParser.Parse(text ?? "", id);

    private static IReadOnlyList<GlossarySpan> Txt(JsonElement block, string id) => Spans(block.Str("txt"), id);

    private static GlossaryBlock Map(JsonElement block, string id)
    {
        var kind = block.Str("t") ?? throw new FormatException($"{id}: bloque sin tipo");
        return kind switch
        {
            "p" => new GlossaryBlock(BlockKind.Paragraph, Text: Txt(block, id)),
            "h" => new GlossaryBlock(BlockKind.Heading, Text: Txt(block, id), Level: block.Int("n") ?? 3),
            "li" => new GlossaryBlock(BlockKind.ListItem, Text: Txt(block, id)),
            "formula" => new GlossaryBlock(BlockKind.Formula, Text: Txt(block, id)),
            "nota" => new GlossaryBlock(BlockKind.Note, Text: Txt(block, id)),
            "tabla" => new GlossaryBlock(BlockKind.Table, Title: block.Str("titulo"), Rows: Rows(block, id),
                Vertical: block.Bool("vertical") || block.Bool("sin_cabecera")),
            "campos" => new GlossaryBlock(BlockKind.Fields, Rows: Rows(block, id)),
            "recuadro" => new GlossaryBlock(BlockKind.Box, Title: block.Str("titulo"),
                Children: MapAll(block.Get("bloques") ?? default, id)),
            "columnas" => new GlossaryBlock(BlockKind.Columns,
                Children: block.Items("items").Select(i => new GlossaryBlock(BlockKind.ListItem, Text: Spans(i.CellText(), id))).ToList()),
            "ficha" => StatBlockBox(block, id),
            _ => throw new FormatException($"{id}: bloque no admitido «{kind}»"),
        };
    }

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<GlossarySpan>>> Rows(JsonElement block, string id) =>
        block.Items("filas")
            .Select(row => (IReadOnlyList<IReadOnlyList<GlossarySpan>>)row.EnumerateArray().Select(c => Spans(c.CellText(), id)).ToList())
            .ToList();

    private static IReadOnlyList<IReadOnlyList<GlossarySpan>> Pair(string label, string? value, string id) =>
        [Spans(label, id), Spans(value, id)];

    /// <summary>A stat block embedded in a spell (summoned creatures) becomes a box.</summary>
    private static GlossaryBlock StatBlockBox(JsonElement ficha, string id)
    {
        var children = new List<GlossaryBlock>
        {
            new(BlockKind.Paragraph, Text: [.. Spans(ficha.Str("tipo"), id).Select(s => s with { Style = s.Style | SpanStyle.Italic })]),
        };

        var fields = new List<IReadOnlyList<IReadOnlyList<GlossarySpan>>>
        {
            Pair("CA", ficha.Str("ca"), id), Pair("PG", ficha.Str("pg"), id), Pair("Velocidad", ficha.Str("vel"), id),
        };
        foreach (var trait in ficha.Items("rasgos"))
            fields.Add(Pair(trait[0].CellText(), trait[1].CellText(), id));
        children.Add(new GlossaryBlock(BlockKind.Fields, Rows: fields));

        var abilities = new List<IReadOnlyList<IReadOnlyList<GlossarySpan>>>
        {
            new[] { "", "Puntuación", "Mod.", "Salv." }.Select(h => Spans(h, id)).ToList(),
        };
        foreach (var row in ficha.Items("car"))
            abilities.Add(row.EnumerateArray().Select(c => Spans(c.CellText(), id)).ToList());
        children.Add(new GlossaryBlock(BlockKind.Table, Rows: abilities));

        foreach (var section in ficha.Items("secciones"))
        {
            children.Add(new GlossaryBlock(BlockKind.Heading, Text: Spans(section.Str("titulo"), id), Level: 4));
            foreach (var item in section.Items("items"))
            {
                List<GlossarySpan> text = [new(item[0].CellText() + ". ", SpanStyle.Bold | SpanStyle.Italic)];
                text.AddRange(Spans(item[1].CellText(), id));
                children.Add(new GlossaryBlock(BlockKind.Paragraph, Text: text));
            }
        }

        return new GlossaryBlock(BlockKind.Box, Title: ficha.Str("nombre"), Children: children);
    }
}
