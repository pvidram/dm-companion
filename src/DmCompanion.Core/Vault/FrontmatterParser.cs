using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace DmCompanion.Core.Vault;

public sealed record FrontmatterResult(IReadOnlyDictionary<string, object?> Fields, string Body, string? Error)
{
    public bool HasError => Error is not null;
}

public static class FrontmatterParser
{
    private static readonly Regex Block = new(
        @"\A---[ \t]*\r?\n(?<yaml>.*?)^---[ \t]*(\r?\n|\z)",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly IDeserializer Yaml = new DeserializerBuilder().Build();

    internal static IReadOnlyDictionary<string, object?> EmptyFields { get; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public static FrontmatterResult Parse(string content)
    {
        var match = Block.Match(content);
        if (!match.Success) return new FrontmatterResult(EmptyFields, content, null);

        var body = content[(match.Index + match.Length)..];
        try
        {
            return Yaml.Deserialize<object?>(match.Groups["yaml"].Value) switch
            {
                null => new FrontmatterResult(EmptyFields, body, null),
                IDictionary<object, object?> map => new FrontmatterResult(ToFields(map), body, null),
                _ => new FrontmatterResult(EmptyFields, body, "El frontmatter no es una lista de propiedades «clave: valor»."),
            };
        }
        catch (YamlException ex)
        {
            return new FrontmatterResult(EmptyFields, body, $"Frontmatter YAML inválido (línea {ex.Start.Line}): {ex.Message}");
        }
    }

    private static IReadOnlyDictionary<string, object?> ToFields(IDictionary<object, object?> map)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in map)
            fields[key?.ToString() ?? string.Empty] = Normalize(value);
        return fields;
    }

    private static object? Normalize(object? value) => value switch
    {
        IDictionary<object, object?> map => ToFields(map),
        IList<object?> list => list.Select(Normalize).ToList(),
        _ => value,
    };
}
