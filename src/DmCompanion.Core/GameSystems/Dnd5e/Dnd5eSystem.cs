using System.Globalization;
using System.Text;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.GameSystems;

public sealed class Dnd5eSystem : IGameSystem
{
    private static readonly string[] AbilityIds = ["str", "dex", "con", "int", "wis", "cha"];
    private static readonly string[] AbilityLabels = ["FUE", "DES", "CON", "INT", "SAB", "CAR"];

    public string Id => "dnd5e";
    public string DisplayName => "D&D 5e (2024)";
    public bool IsKnown => true;
    public SystemDefinition Definition { get; } = BuildDefinition();
    public HitPointsRule HitPoints { get; } = HitPointsRule.FromStat("HP", "hp");
    public GridRules Grid => GridRules.Feet5;
    public IReadOnlyList<GlossaryCategory> GlossaryPacks => Core.Glossary.GlossaryPacks.All;
    public ICombatRules Combat => Dnd5eCombatRules.Instance;

    public StatValues Evaluate(IReadOnlyDictionary<string, object?> frontmatter)
    {
        var stats = FrontmatterReader.GetMap(frontmatter, "stats");
        var values = new Dictionary<string, StatValue>();
        var warnings = new List<string>();

        AddPlain(values, "ac", FrontmatterReader.GetInt(stats, "ac"), signed: false);
        AddPlain(values, "hp", FrontmatterReader.GetInt(stats, "hp"), signed: false);
        AddPlain(values, "speed", FrontmatterReader.GetInt(stats, "speed"), signed: false);

        var abilities = FrontmatterReader.GetMap(stats, "abilities");
        var scores = new Dictionary<string, int>();
        foreach (var id in AbilityIds)
        {
            if (FrontmatterReader.GetInt(abilities, id) is { } score) scores[id] = score;
            values[id] = scores.TryGetValue(id, out var s)
                ? new StatValue(id, $"{s} ({StatFormat.Signed(AbilityModifier.Of(s))})", s, StatSource.Frontmatter)
                : Missing(id, "Falta la característica.");
        }

        values["initiative"] = Initiative(stats, scores);

        var (bonus, bonusReason) = ProficiencyBonus.Resolve(stats);
        var saves = FrontmatterReader.GetMap(stats, "saves");
        foreach (var id in AbilityIds)
            values["save_" + id] = Derive("save_" + id, $"stats.saves.{id}", saves, id, scores, bonus, bonusReason, warnings);

        var skills = FrontmatterReader.GetMap(stats, "skills");
        foreach (var skill in Dnd5eSkills.All)
            values["skill_" + skill.Key] = Derive("skill_" + skill.Key, $"stats.skills.{skill.Key}", skills, skill.Ability, scores, bonus, bonusReason, warnings, skill.Key);

        values["passive_perception"] = PassivePerception(stats, values["skill_perception"]);
        return new StatValues(values, warnings);
    }

    public string StatsTemplate(EntityType type)
    {
        var sb = new StringBuilder();
        sb.Append("portrait:\nstats:\n  ac: 10\n  hp: 10\n  speed: 30\n");
        sb.Append("  abilities: { str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10 }\n");
        sb.Append("  level: 1\n");
        AppendProficiencies(sb);
        return sb.ToString();
    }

    private static readonly IReadOnlyList<CreationField> CreationFields =
    [
        new("ac", "CA", 10, 0, 50, "Básicas"),
        new("hp", "PG", 10, 1, 999, "Básicas"),
        new("speed", "Velocidad", 30, 0, 120, "Básicas"),
        new("level", "Nivel", 1, 1, 20, "Básicas"),
        new("str", "Fuerza", 10, 1, 30, "Características"),
        new("dex", "Destreza", 10, 1, 30, "Características"),
        new("con", "Constitución", 10, 1, 30, "Características"),
        new("int", "Inteligencia", 10, 1, 30, "Características"),
        new("wis", "Sabiduría", 10, 1, 30, "Características"),
        new("cha", "Carisma", 10, 1, 30, "Características"),
    ];

    public IReadOnlyList<CreationField> PcCreationFields => CreationFields;

    public string PcStatsYaml(IReadOnlyDictionary<string, int> values)
    {
        int V(string id) => values.TryGetValue(id, out var v) ? v : CreationFields.First(f => f.Id == id).Default;
        var sb = new StringBuilder();
        sb.Append("portrait:\nstats:\n");
        sb.Append("  ac: ").Append(V("ac")).Append("\n  hp: ").Append(V("hp")).Append("\n  speed: ").Append(V("speed")).Append('\n');
        sb.Append("  abilities: { ").Append(string.Join(", ", AbilityIds.Select(a => $"{a}: {V(a)}"))).Append(" }\n");
        sb.Append("  level: ").Append(V("level")).Append('\n');
        AppendProficiencies(sb);
        return sb.ToString();
    }

    private static void AppendProficiencies(StringBuilder sb)
    {
        sb.Append("  saves: { ").Append(string.Join(", ", AbilityIds.Select(a => a + ": none"))).Append(" }\n");
        sb.Append("  skills:\n");
        foreach (var skill in Dnd5eSkills.All) sb.Append("    ").Append(skill.Key).Append(": none\n");
    }

    private static StatValue Initiative(IReadOnlyDictionary<string, object?> stats, Dictionary<string, int> scores)
    {
        const string id = "initiative";
        if (FrontmatterReader.GetInt(stats, "initiative_bonus") is { } manual)
            return new StatValue(id, StatFormat.Signed(manual), manual, StatSource.Frontmatter);
        if (!scores.TryGetValue("dex", out var dex)) return Missing(id, "Falta el valor.");
        var modifier = AbilityModifier.Of(dex);
        return new StatValue(id, StatFormat.Signed(modifier), modifier, StatSource.Derived);
    }

    private static void AddPlain(Dictionary<string, StatValue> values, string id, int? number, bool signed) =>
        values[id] = number is { } n
            ? new StatValue(id, signed ? StatFormat.Signed(n) : StatFormat.Plain(n), n, StatSource.Frontmatter)
            : Missing(id, "Falta el valor.");

    private static StatValue Missing(string id, string reason) =>
        new(id, StatFormat.Dash, null, StatSource.Missing, reason);

    /// <summary>Salvación o habilidad: un número manda; si no, none|proficient|expertise sobre el modificador.</summary>
    private static StatValue Derive(string id, string path, IReadOnlyDictionary<string, object?> map, string abilityId,
        Dictionary<string, int> scores, int? bonus, string? bonusReason, List<string> warnings, string? key = null)
    {
        var raw = FrontmatterReader.GetString(map, key ?? abilityId);
        if (raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var manual))
            return new StatValue(id, StatFormat.Signed(manual), manual, StatSource.Manual);

        var multiplier = raw?.ToLowerInvariant() switch
        {
            null or "none" => 0,
            "proficient" => 1,
            "expertise" => 2,
            _ => Unknown(path, warnings),
        };

        if (!scores.TryGetValue(abilityId, out var score)) return Missing(id, "Falta la característica.");
        if (multiplier > 0 && bonus is null) return Missing(id, bonusReason!);

        var total = AbilityModifier.Of(score) + multiplier * (bonus ?? 0);
        return new StatValue(id, StatFormat.Signed(total), total, StatSource.Derived);
    }

    private static int Unknown(string path, List<string> warnings)
    {
        warnings.Add($"{path}: valor desconocido; se trata como none.");
        return 0;
    }

    private static StatValue PassivePerception(IReadOnlyDictionary<string, object?> stats, StatValue perception)
    {
        const string id = "passive_perception";
        if (FrontmatterReader.GetInt(stats, id) is { } manual)
            return new StatValue(id, StatFormat.Plain(manual), manual, StatSource.Manual);
        if (perception.Number is not { } p) return new StatValue(id, StatFormat.Dash, null, StatSource.Missing, perception.Reason);
        return new StatValue(id, StatFormat.Plain(10 + p), 10 + p, StatSource.Derived);
    }

    private static SystemDefinition BuildDefinition()
    {
        StatGroup[] groups =
        [
            new("basic", "Básicas", GroupOrder.Definition),
            new("abilities", "Características", GroupOrder.Definition),
            new("saves", "Salvaciones", GroupOrder.Definition),
            new("skills", "Habilidades", GroupOrder.Alphabetical),
        ];

        var stats = new List<StatDefinition>
        {
            new("ac", "stats.ac", "AC", StatKind.Number, "basic", true),
            new("hp", "stats.hp", "HP", StatKind.Number, "basic", true),
            new("speed", "stats.speed", "Velocidad", StatKind.Number, "basic", true),
            new("initiative", "stats.initiative_bonus", "Iniciativa", StatKind.SignedNumber, "basic"),
        };
        for (var i = 0; i < AbilityIds.Length; i++)
            stats.Add(new(AbilityIds[i], $"stats.abilities.{AbilityIds[i]}", AbilityLabels[i], StatKind.AbilityScore, "abilities"));
        for (var i = 0; i < AbilityIds.Length; i++)
            stats.Add(new("save_" + AbilityIds[i], $"stats.saves.{AbilityIds[i]}", AbilityLabels[i], StatKind.Derived, "saves"));
        foreach (var skill in Dnd5eSkills.All)
            stats.Add(new("skill_" + skill.Key, $"stats.skills.{skill.Key}", skill.Label, StatKind.Derived, "skills",
                Abbreviation: AbilityLabels[Array.IndexOf(AbilityIds, skill.Ability)]));
        stats.Add(new("passive_perception", "stats.passive_perception", "Percepción pasiva", StatKind.Derived, "skills", true));

        return new SystemDefinition(groups, stats);
    }
}
