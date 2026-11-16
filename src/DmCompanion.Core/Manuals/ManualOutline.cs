using DmCompanion.Core.Glossary;

namespace DmCompanion.Core.Manuals;

public static class ManualOutline
{
    private sealed class Node(string title, int? page)
    {
        public string Title { get; } = title;
        public int? Page { get; } = page;
        public List<Node> Children { get; } = [];
    }

    public static IReadOnlyList<OutlineItem> Build(IReadOnlyList<OutlineEntry> flat)
    {
        var roots = new List<Node>();
        var path = new List<Node>(); // path[i] = último nodo abierto en el nivel i
        foreach (var entry in flat)
        {
            var depth = Math.Min(Math.Max(entry.Level, 0), path.Count);
            var node = new Node(entry.Title, entry.Page);
            (depth == 0 ? roots : path[depth - 1].Children).Add(node);
            path.RemoveRange(depth, path.Count - depth);
            path.Add(node);
        }
        return roots.Select(ToItem).ToList();
    }

    /// <summary>Conserva los nodos cuyo título contiene el texto (sin tildes ni mayúsculas) y todos sus antecesores; un nodo coincidente conserva todos sus descendientes; con texto vacío, el árbol entero.</summary>
    public static IReadOnlyList<OutlineItem> Filter(IReadOnlyList<OutlineItem> tree, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return tree;
        return FilterNodes(tree, GlossaryText.Normalize(text.Trim()));
    }

    /// <summary>Título de la última sección (en orden de documento) cuya página es ≤ page (para el nombre por defecto de un marcador); null si no hay.</summary>
    public static string? SectionAt(IReadOnlyList<OutlineItem> tree, int page)
    {
        string? found = null;
        Visit(tree);
        return found;

        void Visit(IReadOnlyList<OutlineItem> items)
        {
            foreach (var item in items)
            {
                if (item.Page is { } p && p <= page) found = item.Title;
                Visit(item.Children);
            }
        }
    }

    private static List<OutlineItem> FilterNodes(IReadOnlyList<OutlineItem> items, string needle)
    {
        var kept = new List<OutlineItem>();
        foreach (var item in items)
        {
            if (GlossaryText.Normalize(item.Title).Contains(needle, StringComparison.Ordinal))
            {
                kept.Add(item);
                continue;
            }
            var children = FilterNodes(item.Children, needle);
            if (children.Count > 0) kept.Add(item with { Children = children });
        }
        return kept;
    }

    private static OutlineItem ToItem(Node node) =>
        new(node.Title, node.Page, node.Children.Select(ToItem).ToList());
}
