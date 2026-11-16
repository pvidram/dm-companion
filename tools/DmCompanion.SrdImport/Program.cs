using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

internal static class Program
{
    /// <summary>Pinned on purpose: the importer never deduces it from the folder it reads.</summary>
    public const string SourceCommit = "2d61b5dcabe91ab69d5c4a5a41822eb83c2038cc";

    private static readonly (GlossaryCategory Category, string File, Func<SourceEntry, MappedEntry> Map)[] Sources =
    [
        (GlossaryCategory.Rule, "reglas", TextEntryMapper.Rule),
        (GlossaryCategory.Condition, "estados", TextEntryMapper.Condition),
        (GlossaryCategory.Creature, "criaturas", CreatureMapper.Map),
        (GlossaryCategory.Spell, "conjuros", SpellMapper.Map),
        (GlossaryCategory.MagicItem, "objetos", ItemMapper.Map),
        (GlossaryCategory.Equipment, "equipo", TextEntryMapper.Equipment),
        (GlossaryCategory.CharacterOption, "personajes", TextEntryMapper.CharacterOption),
    ];

    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: DmCompanion.SrdImport <carpeta datos> <carpeta salida>");
            return 1;
        }

        if (!File.Exists(Path.Combine(args[0], "criaturas.json")))
        {
            Console.Error.WriteLine($"No se encontró criaturas.json en «{args[0]}».");
            return 1;
        }

        try
        {
            foreach (var (category, count) in Import(args[0], args[1]))
                Console.WriteLine($"{GlossaryPacks.PackOf(category)}: {count}");
            return 0;
        }
        catch (Exception ex) when (ex is FormatException or IOException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"Error al importar: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Reads the seven source files and writes the eight packs; returns the entry count per category.</summary>
    public static IReadOnlyDictionary<GlossaryCategory, int> Import(string dataDir, string outputDir)
    {
        var packs = new Dictionary<GlossaryCategory, List<MappedEntry>>();
        foreach (var (category, file, map) in Sources)
            packs[category] = SourceReader.Read(Path.Combine(dataDir, file + ".json")).Select(map).ToList();

        var header = new GlossaryPackHeader("compendio-srd", SourceCommit, "5.2.1", "es");
        PackWriter.Write(outputDir, header, packs);
        return packs.ToDictionary(p => p.Key, p => p.Value.Count);
    }
}
