using System.Text.RegularExpressions;

namespace DmCompanion.UI.Tests.Support;

/// <summary>Lee las declaraciones de color de tokens.css y resuelve las referencias var(--x).</summary>
internal static partial class TokenSheet
{
    [GeneratedRegex(@"--(?<name>[a-z0-9-]+)\s*:\s*(?<value>#[0-9A-Fa-f]{6}|var\(--(?<ref>[a-z0-9-]+)\))\s*;")]
    private static partial Regex Declaration();

    public static IReadOnlyDictionary<string, string> Parse(string css)
    {
        var raw = Declaration().Matches(css).ToDictionary(
            m => m.Groups["name"].Value,
            m => m.Groups["ref"].Success ? "var:" + m.Groups["ref"].Value : m.Groups["value"].Value.ToUpperInvariant());
        return raw.Keys.ToDictionary(name => name, name => Resolve(raw, name, depth: 0))
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
    }

    private static string? Resolve(Dictionary<string, string> raw, string name, int depth)
    {
        if (depth > 5 || !raw.TryGetValue(name, out var value)) return null;
        return value.StartsWith("var:", StringComparison.Ordinal) ? Resolve(raw, value[4..], depth + 1) : value;
    }
}
