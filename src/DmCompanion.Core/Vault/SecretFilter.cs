using System.Text;

namespace DmCompanion.Core.Vault;

public readonly record struct TextRange(int Start, int Length);

public static class SecretFilter
{
    private const string Delimiter = "%%";
    private static readonly string[] NotSecretValues = ["false", "no", "off"];

    // Fail-safe: cualquier valor que no sea texto (lista, mapa, bool etiquetado) cuenta como secreto.
    public static bool IsSecret(IReadOnlyDictionary<string, object?> frontmatter) =>
        frontmatter.TryGetValue("secret", out var raw) && raw switch
        {
            null => false,
            string => FrontmatterReader.GetString(frontmatter, "secret") is { } value
                && !NotSecretValues.Contains(value, StringComparer.OrdinalIgnoreCase),
            _ => true,
        };

    public static IReadOnlyList<TextRange> FindSecretRanges(string body) => FindRanges(body, MarkdownCodeMasker.Mask(body));

    private static List<TextRange> FindRanges(string body, string masked)
    {
        var ranges = new List<TextRange>();
        var searchFrom = 0;

        while (searchFrom < masked.Length)
        {
            var open = masked.IndexOf(Delimiter, searchFrom, StringComparison.Ordinal);
            if (open < 0) break;
            var close = masked.IndexOf(Delimiter, open + Delimiter.Length, StringComparison.Ordinal);
            var end = close < 0 ? body.Length : close + Delimiter.Length;
            ranges.Add(new TextRange(open, end - open));
            searchFrom = end;
        }

        return ranges;
    }

    public static string RemoveSecretBlocks(string body) => Remove(body, FindSecretRanges(body));

    // Para jugadores no se confía en el enmascarado de código: Markdig puede considerar texto lo que el
    // enmascarador cree código, así que un `%%` dentro de código también se elimina.
    // Además se une con los rangos enmascarados: sin enmascarar cambia el emparejamiento de `%%` y podría dejar texto secreto.
    public static string RemoveSecretBlocksForPlayers(string body) =>
        Remove(body, Merge(FindSecretRanges(body).Concat(FindRanges(body, body))));

    private static List<TextRange> Merge(IEnumerable<TextRange> ranges)
    {
        var merged = new List<TextRange>();
        foreach (var range in ranges.OrderBy(r => r.Start).ThenByDescending(r => r.Length))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];
                var end = Math.Max(last.Start + last.Length, range.Start + range.Length);
                merged[^1] = new TextRange(last.Start, end - last.Start);
            }
            else merged.Add(range);
        }
        return merged;
    }

    private static string Remove(string body, IReadOnlyList<TextRange> ranges)
    {
        var builder = new StringBuilder(body.Length);
        var cursor = 0;
        foreach (var range in ranges)
        {
            builder.Append(body, cursor, range.Start - cursor);
            cursor = range.Start + range.Length;
        }
        builder.Append(body, cursor, body.Length - cursor);
        return builder.ToString();
    }
}
