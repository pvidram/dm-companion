using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

/// <summary>Rules, conditions, equipment and character options: text blocks plus a subtype.</summary>
internal static class TextEntryMapper
{
    private static readonly Dictionary<string, string> RuleSummaries = new()
    {
        ["regla"] = "Regla", ["glosario"] = "Glosario de reglas", ["veneno"] = "Veneno", ["trampa"] = "Trampa",
    };

    private static readonly Dictionary<string, string> CharacterSummaries = new()
    {
        ["clase"] = "Clase", ["subclase"] = "Subclase", ["especie"] = "Especie", ["trasfondo"] = "Trasfondo", ["dote"] = "Dote",
    };

    public static MappedEntry Condition(SourceEntry source) =>
        Build(GlossaryCategory.Condition, source, null, "Estado");

    public static MappedEntry Rule(SourceEntry source) =>
        Build(GlossaryCategory.Rule, source, source.Tipo, Lookup(RuleSummaries, source));

    public static MappedEntry Equipment(SourceEntry source) =>
        Build(GlossaryCategory.Equipment, source, source.Tipo, source.Es.Str("categoria") ?? "Equipo");

    public static MappedEntry CharacterOption(SourceEntry source) =>
        Build(GlossaryCategory.CharacterOption, source, source.Tipo, Lookup(CharacterSummaries, source));

    private static string Lookup(Dictionary<string, string> labels, SourceEntry source) =>
        source.Tipo is { } tipo && labels.TryGetValue(tipo, out var label)
            ? label
            : throw new FormatException($"{source.Id}: tipo desconocido «{source.Tipo}»");

    private static MappedEntry Build(GlossaryCategory category, SourceEntry source, string? subtype, string summary) =>
        MappedEntry.Create(category, source, subtype, summary, BlockMapper.MapAll(source.Es.Get("bloques") ?? default, source.Id));
}
