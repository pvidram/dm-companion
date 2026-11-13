using DmCompanion.Core.Vault;

namespace DmCompanion.Core.GameSystems;

public interface IGameSystem
{
    string Id { get; }
    string DisplayName { get; }
    bool IsKnown { get; }
    SystemDefinition Definition { get; }
    GridRules Grid { get; }

    /// <summary>De dónde sale el máximo de vida de una nota en este sistema.</summary>
    HitPointsRule HitPoints { get; }

    StatValues Evaluate(IReadOnlyDictionary<string, object?> frontmatter);

    /// <summary>Bloque YAML "stats:" (y claves hermanas) para plantillas pc/npc.</summary>
    string StatsTemplate(EntityType type);
}
