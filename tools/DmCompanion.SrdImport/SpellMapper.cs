using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

internal static class SpellMapper
{
    public static MappedEntry Map(SourceEntry source)
    {
        var id = source.Id;
        var header = source.Es.Str("cabecera") ?? throw new FormatException($"{id}: falta es.cabecera");
        var classes = source.Comun.Items("clases").Select(c => c.GetString() ?? "").ToList();
        var level = source.Comun.Int("nivel") ?? throw new FormatException($"{id}: falta comun.nivel");
        var ritual = source.Comun.Bool("ritual");
        var concentration = source.Comun.Bool("concentracion");

        var spell = new SpellInfo(level, source.Comun.Str("escuela") ?? "", classes, ritual, concentration, header,
            source.Es.Str("tiempo") ?? "", source.Es.Str("alcance") ?? "", source.Es.Str("comp") ?? "", source.Es.Str("dur") ?? "");

        return MappedEntry.Create(GlossaryCategory.Spell, source, null, header, BlockMapper.MapAll(source.Es.Get("bloques") ?? default, id),
            new GlossaryFacets(SpellLevel: level, Classes: classes, Ritual: ritual, Concentration: concentration), spell: spell);
    }
}
