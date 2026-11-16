// PROVISIONAL: pendiente de contrastar con el manual del autor (spec §9.5).
using System.Globalization;
using System.Text;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.GameSystems;

public sealed class CosmereSystem : IGameSystem
{
    private static readonly string[] AttributeIds = ["str", "spd", "int", "wil", "awa", "pre"];
    private static readonly string[] AttributeLabels = ["Fuerza", "Velocidad", "Intelecto", "Voluntad", "Percepción", "Presencia"];
    private static readonly string[] ResourceIds = ["health", "focus", "investiture"];
    private static readonly string[] ResourceLabels = ["Salud", "Concentración", "Investidura"];

    private static readonly (string Id, string Label, string A, string B)[] Defenses =
    [
        ("physical", "Física", "str", "spd"),
        ("cognitive", "Cognitiva", "int", "wil"),
        ("spiritual", "Espiritual", "awa", "pre"),
    ];

    private sealed record Skill(string Key, string Label, string Attribute);

    // Orden de la tabla del brief, fila a fila de izquierda a derecha.
    private static readonly Skill[] Skills =
    [
        new("agility", "Agilidad", "spd"), new("athletics", "Atletismo", "str"),
        new("heavy_weaponry", "Armas pesadas", "str"), new("light_weaponry", "Armas ligeras", "spd"),
        new("stealth", "Sigilo", "spd"), new("thievery", "Latrocinio", "spd"),
        new("crafting", "Artesanía", "int"), new("deduction", "Deducción", "int"),
        new("discipline", "Disciplina", "wil"), new("intimidation", "Intimidación", "wil"),
        new("lore", "Saber", "int"), new("medicine", "Medicina", "int"),
        new("deception", "Engaño", "pre"), new("insight", "Perspicacia", "awa"),
        new("leadership", "Liderazgo", "pre"), new("perception", "Percepción", "awa"),
        new("persuasion", "Persuasión", "pre"), new("survival", "Supervivencia", "awa"),
    ];

    public string Id => "cosmere";
    public string DisplayName => "Cosmere RPG";
    public bool IsKnown => true;
    public SystemDefinition Definition { get; } = BuildDefinition();
    public HitPointsRule HitPoints { get; } = HitPointsRule.FromStat("Salud", "health");
    public GridRules Grid => GridRules.Feet5;
    public IReadOnlyList<GlossaryCategory> GlossaryPacks => [];
    public ICombatRules Combat => BasicCombatRules.Instance;

    public StatValues Evaluate(IReadOnlyDictionary<string, object?> frontmatter)
    {
        var stats = FrontmatterReader.GetMap(frontmatter, "stats");
        var values = new Dictionary<string, StatValue>();
        var warnings = new List<string>();

        var attributes = FrontmatterReader.GetMap(stats, "attributes");
        var scores = new Dictionary<string, int>();
        foreach (var id in AttributeIds)
        {
            if (FrontmatterReader.GetInt(attributes, id) is { } score)
            {
                scores[id] = score;
                values[id] = new StatValue(id, StatFormat.Plain(score), score, StatSource.Frontmatter);
            }
            else values[id] = Missing(id, "Falta el atributo.");
        }

        var manualDefenses = FrontmatterReader.GetMap(stats, "defenses");
        foreach (var (id, _, a, b) in Defenses)
            values[id] = Defense(id, manualDefenses, scores, a, b);

        foreach (var id in ResourceIds) values[id] = Resource(id, stats);

        foreach (var id in new[] { "deflect", "movement" })
            values[id] = FrontmatterReader.GetInt(stats, id) is { } n
                ? new StatValue(id, StatFormat.Plain(n), n, StatSource.Frontmatter)
                : Missing(id, "Falta el valor.");

        var ranks = FrontmatterReader.GetMap(stats, "skills");
        foreach (var skill in Skills)
            values[skill.Key] = SkillValue(skill, ranks, scores, warnings);

        return new StatValues(values, warnings);
    }

    public string StatsTemplate(EntityType type)
    {
        var sb = new StringBuilder();
        sb.Append("stats:\n");
        sb.Append("  attributes: { ").Append(string.Join(", ", AttributeIds.Select(a => a + ": 0"))).Append(" }\n");
        sb.Append("  defenses: {}\n");
        foreach (var id in ResourceIds) sb.Append("  ").Append(id).Append(": { current: 0, max: 0 }\n");
        sb.Append("  deflect: 0\n  movement: 0\n  skills:\n");
        foreach (var skill in Skills) sb.Append("    ").Append(skill.Key).Append(": 0\n");
        return sb.ToString();
    }

    private static StatValue Missing(string id, string reason) =>
        new(id, StatFormat.Dash, null, StatSource.Missing, reason);

    private static StatValue Defense(string id, IReadOnlyDictionary<string, object?> manual,
        Dictionary<string, int> scores, string a, string b)
    {
        if (FrontmatterReader.GetInt(manual, id) is { } m)
            return new StatValue(id, StatFormat.Plain(m), m, StatSource.Manual);
        if (!scores.TryGetValue(a, out var x) || !scores.TryGetValue(b, out var y))
            return Missing(id, "Falta el atributo.");
        return new StatValue(id, StatFormat.Plain(10 + x + y), 10 + x + y, StatSource.Derived);
    }

    private static StatValue Resource(string id, IReadOnlyDictionary<string, object?> stats)
    {
        var raw = FrontmatterReader.GetPath(stats, id);
        if (raw is IReadOnlyDictionary<string, object?> map)
        {
            var max = FrontmatterReader.GetInt(map, "max");
            if (max is null) return Missing(id, "Falta el máximo.");
            var current = FrontmatterReader.GetInt(map, "current") ?? max.Value;
            return new StatValue(id, $"{StatFormat.Plain(current)}/{StatFormat.Plain(max)}", max, StatSource.Frontmatter);
        }
        return FrontmatterReader.GetInt(stats, id) is { } n
            ? new StatValue(id, StatFormat.Plain(n), n, StatSource.Frontmatter)
            : Missing(id, "Falta el valor.");
    }

    private static StatValue SkillValue(Skill skill, IReadOnlyDictionary<string, object?> ranks,
        Dictionary<string, int> scores, List<string> warnings)
    {
        var rank = Rank(skill.Key, ranks, warnings);
        if (!scores.TryGetValue(skill.Attribute, out var score)) return Missing(skill.Key, "Falta el atributo.");
        var total = score + rank;
        return new StatValue(skill.Key, StatFormat.Signed(total), total, StatSource.Derived);
    }

    private static int Rank(string key, IReadOnlyDictionary<string, object?> ranks, List<string> warnings)
    {
        if (!ranks.TryGetValue(key, out var raw) || raw is null) return 0;
        if (FrontmatterReader.GetInt(ranks, key) is { } n and >= 0 and <= 5) return n;
        warnings.Add($"stats.skills.{key}: el rango debe ser un entero de 0 a 5; se cuenta como 0.");
        return 0;
    }

    private static SystemDefinition BuildDefinition()
    {
        StatGroup[] groups =
        [
            new("basic", "Básicas", GroupOrder.Definition),
            new("attributes", "Atributos", GroupOrder.Definition),
            new("defenses", "Defensas", GroupOrder.Definition),
            new("resources", "Recursos", GroupOrder.Definition),
            new("skills", "Habilidades", GroupOrder.Alphabetical),
        ];

        var stats = new List<StatDefinition>();
        for (var i = 0; i < AttributeIds.Length; i++)
            stats.Add(new(AttributeIds[i], $"stats.attributes.{AttributeIds[i]}", AttributeLabels[i], StatKind.Number, "attributes"));
        foreach (var (id, label, _, _) in Defenses)
            stats.Add(new(id, $"stats.defenses.{id}", label, StatKind.Derived, "defenses"));
        stats.Add(new("deflect", "stats.deflect", "Desvío", StatKind.Number, "defenses"));
        stats.Add(new("movement", "stats.movement", "Movimiento", StatKind.Number, "basic"));
        for (var i = 0; i < ResourceIds.Length; i++)
            stats.Add(new(ResourceIds[i], $"stats.{ResourceIds[i]}", ResourceLabels[i], StatKind.Resource, "resources"));
        foreach (var skill in Skills)
            stats.Add(new(skill.Key, $"stats.skills.{skill.Key}", skill.Label, StatKind.Derived, "skills",
                Abbreviation: AttributeLabels[Array.IndexOf(AttributeIds, skill.Attribute)][..3].ToUpperInvariant()));

        return new SystemDefinition(groups, stats);
    }
}
