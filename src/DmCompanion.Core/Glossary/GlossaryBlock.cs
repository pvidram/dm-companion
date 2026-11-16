namespace DmCompanion.Core.Glossary;

/// <summary>Bloque de texto del glosario. Nunca contiene HTML: el texto va en tramos con estilo.</summary>
public sealed record GlossaryBlock(
    BlockKind Kind,
    IReadOnlyList<GlossarySpan>? Text = null,                      // Paragraph, Heading, ListItem, Formula, Note
    int? Level = null,                                             // Heading: 1..4
    string? Title = null,                                          // Table, Box
    IReadOnlyList<IReadOnlyList<IReadOnlyList<GlossarySpan>>>? Rows = null, // Table (fila 0 = cabecera salvo Vertical), Fields (2 celdas)
    bool Vertical = false,                                         // Table: pares etiqueta/valor sin cabecera
    IReadOnlyList<GlossaryBlock>? Children = null);                // Box, Columns (Children = ListItem)
