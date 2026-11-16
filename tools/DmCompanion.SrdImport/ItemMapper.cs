using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

internal static class ItemMapper
{
    public static MappedEntry Map(SourceEntry source)
    {
        var id = source.Id;
        var header = source.Es.Str("cabecera") ?? throw new FormatException($"{id}: falta es.cabecera");
        var rarities = source.Comun.Items("rarezas").Select(r => r.GetString() ?? "").ToList();
        var item = new ItemInfo(source.Comun.Str("categoria") ?? "", rarities, source.Comun.Bool("sintonizacion"), header);

        return MappedEntry.Create(GlossaryCategory.MagicItem, source, null, header, BlockMapper.MapAll(source.Es.Get("bloques") ?? default, id),
            new GlossaryFacets(Rarities: rarities), item: item);
    }
}
