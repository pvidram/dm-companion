using DmCompanion.Core.Glossary;
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

    /// <summary>Reglas de combate del sistema (condiciones, vida temporal, concentración).</summary>
    ICombatRules Combat { get; }

    /// <summary>Paquetes del SRD que usa el glosario con este sistema.</summary>
    IReadOnlyList<GlossaryCategory> GlossaryPacks { get; }

    StatValues Evaluate(IReadOnlyDictionary<string, object?> frontmatter);

    /// <summary>Bloque YAML "stats:" (y claves hermanas) para plantillas pc/npc.</summary>
    string StatsTemplate(EntityType type);
}
