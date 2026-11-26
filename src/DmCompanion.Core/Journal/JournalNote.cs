using System.Globalization;
using System.Text;
using YamlDotNet.Serialization;

namespace DmCompanion.Core.Journal;

public static class JournalNote
{
    private static readonly HashSet<string> Known = new(["type", "added", "images", "creature"], StringComparer.OrdinalIgnoreCase);
    private static readonly ISerializer Yaml = new SerializerBuilder().Build();

    /// <summary>Texto de la nota. Las claves de <paramref name="previousFields"/> que no son del diario (las que el DM añadió en Obsidian) se conservan.</summary>
    public static string Compose(DateTimeOffset added, IReadOnlyList<string> images, string? creature, string? note,
        IReadOnlyDictionary<string, object?>? previousFields = null)
    {
        var text = new StringBuilder("---\ntype: journal\n");
        text.Append("added: ").Append(added.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)).Append('\n');
        text.Append("images:\n");
        foreach (var image in images) text.Append("  - ").Append(Quote(image)).Append('\n');
        if (!string.IsNullOrWhiteSpace(creature)) text.Append("creature: ").Append(Quote(creature.Trim())).Append('\n');
        var extras = previousFields?.Where(pair => !Known.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (extras is { Count: > 0 }) text.Append(Yaml.Serialize(extras));
        text.Append("---\n");
        if (!string.IsNullOrWhiteSpace(note)) text.Append(note.Trim().Replace("\r\n", "\n")).Append('\n');
        return text.ToString();
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
