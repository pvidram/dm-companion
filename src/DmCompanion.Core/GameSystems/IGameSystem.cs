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

    /// <summary>Estadísticas base que se piden al crear un PJ, en orden de formulario.</summary>
    IReadOnlyList<CreationField> PcCreationFields { get; }

    /// <summary>
    /// Bloque YAML "stats:" (y claves hermanas) de un PJ nuevo, terminado en salto de línea.
    /// Una id ausente en <paramref name="values"/> usa su valor por defecto; las desconocidas se ignoran.
    /// </summary>
    string PcStatsYaml(IReadOnlyDictionary<string, int> values);
}
