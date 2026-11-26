using System.Globalization;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Journal;

public static class JournalReader
{
    public const string TypeKey = "journal";

    public static bool IsJournal(IReadOnlyDictionary<string, object?> fields) =>
        string.Equals(FrontmatterReader.GetString(fields, "type"), TypeKey, StringComparison.OrdinalIgnoreCase);

    public static JournalEntry Read(string path, string content, DateTime lastWriteUtc, Func<string, bool> fileExists)
    {
        var parsed = FrontmatterParser.Parse(content);
        return Read(path, parsed.Fields, parsed.Body, parsed.Error, lastWriteUtc, fileExists);
    }

    public static JournalEntry Read(string path, IReadOnlyDictionary<string, object?> fields, string body, string? parseError,
        DateTime lastWriteUtc, Func<string, bool> fileExists)
    {
        var normalized = VaultPaths.Normalize(path);
        var folder = JournalPaths.FolderOf(normalized);
        var warnings = new List<string>();
        if (parseError is not null) warnings.Add(parseError);
        var added = ReadAdded(fields, lastWriteUtc, parseError is null, warnings);
        var images = ReadImages(fields, folder, fileExists, warnings);
        return new JournalEntry(
            normalized, JournalNames.TitleOf(VaultPaths.FileTitle(normalized)), JournalPaths.OwnerNameOf(normalized), folder,
            added, images, ReadCreature(fields), body.Trim(), warnings,
            // Fail-safe, como VaultEntity: con YAML inválido se pierde `secret: true`, así que se trata como secreta.
            IsSecret: parseError is not null || SecretFilter.IsSecret(fields));
    }

    private static DateTimeOffset ReadAdded(IReadOnlyDictionary<string, object?> fields, DateTime lastWriteUtc, bool warn, List<string> warnings)
    {
        if (fields.TryGetValue("added", out var raw))
        {
            switch (raw)
            {
                case DateTimeOffset offset: return offset;
                case DateTime date: return new DateTimeOffset(date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date);
                case string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed): return parsed;
            }
        }
        if (warn) warnings.Add("Falta la fecha «added» o no es válida; se usa la fecha de modificación del fichero.");
        return new DateTimeOffset(DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc));
    }

    private static IReadOnlyList<JournalImage> ReadImages(
        IReadOnlyDictionary<string, object?> fields, string folder, Func<string, bool> fileExists, List<string> warnings)
    {
        if (!fields.TryGetValue("images", out var raw) || raw is not IEnumerable<object?> list) return [];
        var images = new List<JournalImage>();
        foreach (var item in list)
        {
            var name = (item as string)?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains('/') || name.Contains('\\') || name.Contains(':') || name.StartsWith('.'))
            {
                warnings.Add($"Nombre de imagen no válido: {name}");
                continue;
            }
            var imagePath = folder.Length == 0 ? name : $"{folder}/{name}";
            var exists = fileExists(imagePath);
            if (!exists) warnings.Add($"Imagen no encontrada: {name}");
            images.Add(new JournalImage(name, imagePath, exists, JournalPaths.IsDisplayable(name)));
        }
        return images;
    }

    // `creature: [[Ankheg]]` sin comillas es, para YAML, una lista dentro de otra lista.
    private static string? ReadCreature(IReadOnlyDictionary<string, object?> fields)
    {
        if (!fields.TryGetValue("creature", out var raw)) return null;
        return raw switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
            IEnumerable<object?> outer when outer.FirstOrDefault() is IEnumerable<object?> inner && inner.FirstOrDefault() is string name
                && !string.IsNullOrWhiteSpace(name) => $"[[{name.Trim()}]]",
            _ => null,
        };
    }
}
