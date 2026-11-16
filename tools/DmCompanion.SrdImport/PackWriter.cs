using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

internal static class PackWriter
{
    // Same shape as GlossaryJson.Options, but accents are written as text instead of \uXXXX escapes.
    private static readonly JsonSerializerOptions Options = new(GlossaryJson.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void Write(string outputDir, GlossaryPackHeader header, IReadOnlyDictionary<GlossaryCategory, List<MappedEntry>> packs)
    {
        Directory.CreateDirectory(outputDir);
        var listings = new List<GlossaryListing>();

        foreach (var (category, mapped) in packs)
        {
            var sorted = mapped.OrderBy(m => m.Entry.Key, StringComparer.Ordinal).ToList();
            listings.AddRange(sorted.Select(m => m.Listing));
            WriteFile(outputDir, GlossaryPacks.PackOf(category),
                new GlossaryEntriesFile(header, category, sorted.Select(m => m.Entry).ToList()));
        }

        var index = new GlossaryIndexFile(header, listings.OrderBy(l => l.Key, StringComparer.Ordinal).ToList());
        WriteFile(outputDir, GlossaryPacks.IndexPack, index);
    }

    private static void WriteFile<T>(string outputDir, string name, T file)
    {
        var json = JsonSerializer.Serialize(file, Options);
        File.WriteAllText(Path.Combine(outputDir, name + ".json"), json + "\n", Utf8NoBom);
    }
}
