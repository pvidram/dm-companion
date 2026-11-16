using System.Globalization;
using System.Text.Json;

namespace DmCompanion.SrdImport;

/// <summary>One entry of a source file: only the Spanish text, the English name and the shared data are read.</summary>
internal sealed record SourceEntry(string Id, string? Tipo, JsonElement Comun, JsonElement Es, string? NameEn)
{
    public string Name => Es.Str("nombre") ?? throw new FormatException($"{Id}: falta es.nombre");

    public int? Page => Es.Int("p");
}

internal static class SourceReader
{
    public static IReadOnlyList<SourceEntry> Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new FormatException($"{Path.GetFileName(path)}: se esperaba una lista de entradas");

        var entries = new List<SourceEntry>();
        foreach (var raw in doc.RootElement.EnumerateArray())
        {
            var id = raw.Str("id") ?? throw new FormatException($"{Path.GetFileName(path)}: entrada sin id");
            if (!raw.TryGetProperty("es", out var es)) throw new FormatException($"{id}: falta el bloque es");
            var comun = raw.TryGetProperty("comun", out var c) ? c.Clone() : default;
            var nameEn = raw.TryGetProperty("en", out var en) ? en.Str("nombre") : null;
            entries.Add(new SourceEntry(id, raw.Str("tipo"), comun, es.Clone(), nameEn));
        }

        return entries;
    }
}

internal static class JsonElementExtensions
{
    public static JsonElement? Get(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    public static string? Str(this JsonElement element, string name) =>
        element.Get(name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static int? Int(this JsonElement element, string name) =>
        element.Get(name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : null;

    public static bool Bool(this JsonElement element, string name) =>
        element.Get(name) is { ValueKind: JsonValueKind.True };

    public static IEnumerable<JsonElement> Items(this JsonElement element, string name) =>
        element.Get(name) is { ValueKind: JsonValueKind.Array } v ? v.EnumerateArray() : [];

    /// <summary>Text or number cell; numbers are written with the invariant culture.</summary>
    public static string CellText(this JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.String => cell.GetString() ?? "",
        JsonValueKind.Number => cell.GetRawText(),
        JsonValueKind.Null => "",
        _ => throw new FormatException($"Celda con un valor no admitido ({cell.ValueKind.ToString().ToLower(CultureInfo.InvariantCulture)})"),
    };
}
