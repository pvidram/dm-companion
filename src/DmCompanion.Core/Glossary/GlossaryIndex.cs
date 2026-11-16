using System.Globalization;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Glossary;

/// <summary>Índice inmutable del glosario: filas del SRD más las notas del vault, que tapan a las del SRD del mismo nombre.</summary>
public sealed class GlossaryIndex
{
    private const int MaxResults = 200;
    private const string YourNote = "Tu nota";

    private static readonly StringComparer NameComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), CompareOptions.IgnoreCase);

    private static readonly GlossaryCategory[] ResolveOrder =
    [
        GlossaryCategory.Condition, GlossaryCategory.Rule, GlossaryCategory.Spell, GlossaryCategory.MagicItem,
        GlossaryCategory.Equipment, GlossaryCategory.CharacterOption, GlossaryCategory.Creature,
    ];

    private readonly record struct Searchable(GlossaryListing Row, string Name, string? NameEn);

    private readonly IReadOnlyDictionary<string, GlossaryListing> _byKey;
    private readonly IReadOnlyDictionary<string, GlossaryListing> _overriddenBy;
    private readonly IReadOnlyList<Searchable> _visible;
    private readonly IReadOnlyDictionary<string, GlossaryListing> _resolvable;

    private GlossaryIndex(
        IReadOnlyDictionary<string, GlossaryListing> byKey, IReadOnlyDictionary<string, GlossaryListing> overriddenBy,
        IReadOnlyList<Searchable> visible, IReadOnlyDictionary<string, GlossaryListing> resolvable,
        IReadOnlyCollection<string> conditionKeys)
    {
        _byKey = byKey;
        _overriddenBy = overriddenBy;
        _visible = visible;
        _resolvable = resolvable;
        ConditionKeys = conditionKeys;
    }

    public static GlossaryIndex Empty { get; } = Build([], VaultIndex.Empty);

    public IReadOnlyCollection<string> ConditionKeys { get; }

    /// <param name="srd">Filas del índice del SRD ya filtradas por el sistema.</param>
    /// <param name="vault">Índice del vault abierto.</param>
    public static GlossaryIndex Build(IReadOnlyList<GlossaryListing> srd, VaultIndex vault)
    {
        var srdByName = new Dictionary<(GlossaryCategory, string), List<GlossaryListing>>();
        foreach (var row in srd)
        {
            AddName(srdByName, row, row.Name);
            if (row.NameEn is not null) AddName(srdByName, row, row.NameEn);
        }

        var srdByKey = new Dictionary<string, GlossaryListing>(StringComparer.Ordinal);
        foreach (var row in srd) srdByKey[row.Key] = row;

        var byKey = new Dictionary<string, GlossaryListing>(StringComparer.Ordinal);
        var overriddenBy = new Dictionary<string, GlossaryListing>(StringComparer.Ordinal);
        var vaultRows = new List<GlossaryListing>();

        foreach (var entity in vault.Entities.OrderBy(e => e.RelativePath, StringComparer.Ordinal))
        {
            if (VaultRow(entity) is not { } row) continue;
            var covered = SourceRow(entity, srdByKey) is { } fromSource
                ? [fromSource]
                : srdByName.GetValueOrDefault((row.Category, GlossaryText.Normalize(entity.Title)))
                    ?.OrderBy(r => r.Key, StringComparer.Ordinal).ToList() ?? [];
            if (covered.Count > 0) row = row with { Overrides = covered[0].Key };
            foreach (var srdRow in covered) overriddenBy.TryAdd(srdRow.Key, row);
            vaultRows.Add(row);
        }

        foreach (var row in srd) byKey[row.Key] = row;
        foreach (var row in vaultRows) byKey[row.Key] = row;
        var visibleSrd = srd.Where(r => !overriddenBy.ContainsKey(r.Key)).ToList();
        var visible = visibleSrd.Concat(vaultRows).Select(r => new Searchable(
            r, GlossaryText.Normalize(r.Name), r.NameEn is null ? null : GlossaryText.Normalize(r.NameEn))).ToList();

        var resolvable = new Dictionary<string, GlossaryListing>(StringComparer.Ordinal);
        foreach (var category in ResolveOrder)
            foreach (var row in visibleSrd.Where(r => r.Category == category).OrderBy(r => r.Key, StringComparer.Ordinal))
                foreach (var name in new[] { row.Name, row.NameEn })
                    if (name is not null) resolvable.TryAdd(ResolveKey(category, name), row);

        var conditionKeys = srd.Where(r => r.Category == GlossaryCategory.Condition).Select(r => r.Key).ToList();
        return new GlossaryIndex(byKey, overriddenBy, visible, resolvable, conditionKeys);
    }

    public IReadOnlyList<GlossaryListing> Search(GlossaryQuery query)
    {
        var text = string.IsNullOrWhiteSpace(query.Text) ? null : GlossaryText.Normalize(query.Text.Trim());
        var matches = new List<(Searchable Item, int Rank)>();
        foreach (var item in _visible)
        {
            if (query.Category is { } category && item.Row.Category != category) continue;
            if (query.Filter is { } filter && !Passes(item.Row, filter)) continue;
            var rank = text is null ? 0 : Rank(item, text);
            if (rank >= 0) matches.Add((item, rank));
        }
        return matches
            .OrderBy(m => m.Rank)
            .ThenBy(m => m.Item.Row.Name, NameComparer)
            .ThenBy(m => m.Item.Row.Key, StringComparer.Ordinal)
            .Take(MaxResults)
            .Select(m => m.Item.Row)
            .ToList();
    }

    public GlossaryListing? Get(string key) => _byKey.GetValueOrDefault(key);

    public GlossaryListing? OverriddenBy(string srdKey) => _overriddenBy.GetValueOrDefault(srdKey);

    /// <summary>Solo SRD no tapado; primero estados, luego reglas, conjuros, objetos, equipo, personajes y criaturas.</summary>
    public GlossaryListing? TryResolve(string name)
    {
        var normalized = GlossaryText.Normalize(name);
        foreach (var category in ResolveOrder)
            if (_resolvable.TryGetValue(ResolveKey(category, normalized), out var row)) return row;
        return null;
    }

    private static string ResolveKey(GlossaryCategory category, string name) => $"{(int)category}|{GlossaryText.Normalize(name)}";

    private static void AddName(Dictionary<(GlossaryCategory, string), List<GlossaryListing>> map, GlossaryListing row, string name)
    {
        var key = (row.Category, GlossaryText.Normalize(name));
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        if (!list.Contains(row)) list.Add(row);
    }

    /// <summary>Fila del SRD a la que apunta el <c>source</c> de la nota (lo escribe «Copiar al vault»); null si no hay o no existe.</summary>
    private static GlossaryListing? SourceRow(VaultEntity entity, IReadOnlyDictionary<string, GlossaryListing> srdByKey) =>
        FrontmatterReader.GetString(entity.Frontmatter, "source") is { } source
        && source.StartsWith(GlossaryPacks.SrdPrefix, StringComparison.Ordinal)
            ? srdByKey.GetValueOrDefault(source)
            : null;

    private static GlossaryListing? VaultRow(VaultEntity entity)
    {
        var category = entity.Type switch
        {
            EntityType.Rule => GlossaryCategory.Rule,
            EntityType.Monster => GlossaryCategory.Creature,
            _ => (GlossaryCategory?)null,
        };
        if (category is null) return null;

        string? cr = null;
        if (category == GlossaryCategory.Creature)
            cr = FrontmatterReader.GetString(FrontmatterReader.GetMap(entity.Frontmatter, "stats"), "cr");
        var summary = cr is null ? YourNote : $"VD {cr} · {YourNote}";
        return new GlossaryListing(
            GlossaryPacks.VaultKey(entity.RelativePath), category.Value, null, entity.Title, null, summary,
            new GlossaryFacets(Cr: cr), entity.RelativePath);
    }

    private static int Rank(Searchable item, string text)
    {
        var best = -1;
        foreach (var name in new[] { item.Name, item.NameEn })
        {
            if (name is null) continue;
            var rank = name == text ? 0 : name.StartsWith(text, StringComparison.Ordinal) ? 1
                : name.Contains(text, StringComparison.Ordinal) ? 2 : -1;
            if (rank >= 0 && (best < 0 || rank < best)) best = rank;
        }
        return best;
    }

    private static bool Passes(GlossaryListing row, GlossaryFilter filter)
    {
        var facets = row.Facets;
        if (filter.CrMin is not null || filter.CrMax is not null)
        {
            if (string.IsNullOrWhiteSpace(facets.Cr)) return false;
            var cr = GlossaryLabels.CrValue(facets.Cr);
            if (cr < filter.CrMin || cr > filter.CrMax) return false;
        }
        return (filter.CreatureType is null || facets.CreatureType == filter.CreatureType)
            && (filter.SpellLevel is null || facets.SpellLevel == filter.SpellLevel)
            && (filter.SpellClass is null || facets.Classes?.Contains(filter.SpellClass) == true)
            && (filter.Ritual is null || facets.Ritual == filter.Ritual)
            && (filter.Concentration is null || facets.Concentration == filter.Concentration)
            && (filter.Rarity is null || facets.Rarities?.Contains(filter.Rarity) == true);
    }
}
