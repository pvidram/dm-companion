using System.Globalization;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.GameSystems;

public sealed class GenericSystem(string id) : IGameSystem
{
    public string Id { get; } = id;
    public string DisplayName => Id;
    public bool IsKnown => false;
    public SystemDefinition Definition => SystemDefinition.Empty;
    public HitPointsRule HitPoints { get; } = HitPointsRule.FromStat("HP", "stats.hp");
    public GridRules Grid => GridRules.Feet5;
    public IReadOnlyList<GlossaryCategory> GlossaryPacks => [];
    public ICombatRules Combat => BasicCombatRules.Instance;

    public StatValues Evaluate(IReadOnlyDictionary<string, object?> frontmatter)
    {
        var values = new Dictionary<string, StatValue>();
        if (FrontmatterReader.GetMap(frontmatter, "stats") is { Count: > 0 } stats)
            AddLeaves("stats", stats, values);
        return new StatValues(values, [$"Sistema «{Id}» desconocido: se muestra la vista genérica."]);
    }

    public string StatsTemplate(EntityType type) => "stats: {}\n";

    private static void AddLeaves(string path, IReadOnlyDictionary<string, object?> map, Dictionary<string, StatValue> values)
    {
        foreach (var (key, value) in map)
        {
            var childPath = $"{path}.{key}";
            if (value is IReadOnlyDictionary<string, object?> child) AddLeaves(childPath, child, values);
            else values[childPath] = new StatValue(childPath, ToText(value), AsInt(value), StatSource.Frontmatter);
        }
    }

    private static int? AsInt(object? value) =>
        value is string or IFormattable && int.TryParse(ToText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string ToText(object? value) => value switch
    {
        null => string.Empty,
        IEnumerable<object?> list => string.Join(", ", list.Select(ToText)),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
