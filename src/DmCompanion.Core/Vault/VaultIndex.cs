using System.Globalization;

namespace DmCompanion.Core.Vault;

public sealed class VaultIndex
{
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions LooseMatch = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly Dictionary<string, VaultEntity> _byPath;
    private readonly Dictionary<string, VaultEntity> _byFileTitle;
    private readonly Dictionary<string, List<VaultEntity>> _backlinks;

    private VaultIndex(IEnumerable<VaultEntity> entities)
    {
        _byPath = new Dictionary<string, VaultEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities) _byPath[entity.RelativePath] = entity;

        _byFileTitle = _byPath.Values
            .GroupBy(entity => entity.FileTitle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => ShortestPath(group), StringComparer.OrdinalIgnoreCase);

        _backlinks = BuildBacklinks();
    }

    public static VaultIndex Empty { get; } = new([]);

    public static VaultIndex Build(IEnumerable<VaultEntity> entities) => new(entities);

    public int Count => _byPath.Count;

    public IReadOnlyCollection<VaultEntity> Entities => _byPath.Values;

    public VaultEntity? Get(string relativePath) => _byPath.GetValueOrDefault(VaultPaths.Normalize(relativePath));

    public VaultEntity? Resolve(string target)
    {
        var key = VaultPaths.LinkKey(target.Trim());
        if (!key.Contains('/')) return _byFileTitle.GetValueOrDefault(key);

        var candidates = _byPath.Values.Where(entity =>
            entity.LinkKey.Equals(key, StringComparison.OrdinalIgnoreCase)
            || entity.LinkKey.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase));
        return candidates.Any() ? ShortestPath(candidates) : null;
    }

    public bool IsBroken(Wikilink link) => Resolve(link.Target) is null;

    public IReadOnlyList<VaultEntity> GetBacklinks(string relativePath) =>
        _backlinks.TryGetValue(VaultPaths.Normalize(relativePath), out var sources) ? ByTitle(sources) : [];

    public IReadOnlyList<VaultEntity> OfType(EntityType type) => ByTitle(_byPath.Values.Where(entity => entity.Type == type));

    public IReadOnlyList<VaultEntity> Search(string query)
    {
        var text = query.Trim();
        if (text.Length == 0) return [];

        var titleMatches = ByTitle(_byPath.Values.Where(entity => Contains(entity.Title, text)));
        var bodyMatches = ByTitle(_byPath.Values.Where(entity => !Contains(entity.Title, text) && Contains(entity.Body, text)));
        return [.. titleMatches, .. bodyMatches];
    }

    public VaultIndex Upsert(VaultEntity entity) =>
        new(_byPath.Values.Where(existing => !SamePath(existing, entity.RelativePath)).Append(entity));

    public VaultIndex Remove(string relativePath) =>
        new(_byPath.Values.Where(existing => !SamePath(existing, VaultPaths.Normalize(relativePath))));

    private Dictionary<string, List<VaultEntity>> BuildBacklinks()
    {
        var backlinks = new Dictionary<string, List<VaultEntity>>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in _byPath.Values)
        {
            var targets = source.Links
                .Where(link => !link.IsEmbed)
                .Select(link => Resolve(link.Target))
                .OfType<VaultEntity>()
                .Where(target => !SamePath(target, source.RelativePath))
                .Select(target => target.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                if (!backlinks.TryGetValue(target, out var sources)) backlinks[target] = sources = [];
                sources.Add(source);
            }
        }
        return backlinks;
    }

    private static VaultEntity ShortestPath(IEnumerable<VaultEntity> entities) =>
        entities.OrderBy(entity => entity.RelativePath.Length).ThenBy(entity => entity.RelativePath, StringComparer.Ordinal).First();

    private static List<VaultEntity> ByTitle(IEnumerable<VaultEntity> entities) =>
        entities.OrderBy(entity => entity.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

    private static bool SamePath(VaultEntity entity, string relativePath) =>
        entity.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string text, string query) => Compare.IndexOf(text, query, LooseMatch) >= 0;
}
