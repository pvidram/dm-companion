using System.Globalization;

namespace DmCompanion.Core.Vault;

public static class FrontmatterReader
{
    public static string? GetString(IReadOnlyDictionary<string, object?> fields, string key) =>
        fields.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    public static int? GetInt(IReadOnlyDictionary<string, object?> fields, string key) =>
        GetString(fields, key) is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    public static bool GetBool(IReadOnlyDictionary<string, object?> fields, string key) =>
        GetString(fields, key) is { } text && bool.TryParse(text, out var flag) && flag;

    public static IReadOnlyDictionary<string, object?> GetMap(IReadOnlyDictionary<string, object?> fields, string key) =>
        fields.TryGetValue(key, out var value) && value is IReadOnlyDictionary<string, object?> map
            ? map
            : FrontmatterParser.EmptyFields;

    public static object? GetPath(IReadOnlyDictionary<string, object?> fields, string path)
    {
        object? current = fields;
        foreach (var segment in path.Split('.'))
        {
            if (current is not IReadOnlyDictionary<string, object?> map || !map.TryGetValue(segment, out current))
                return null;
        }
        return current;
    }
}
