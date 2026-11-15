using DmCompanion.Core.Combat;

namespace DmCompanion.Core.GameSystems;

/// <summary>Condición del catálogo de un sistema. MaxLevel != null indica condición con niveles (p. ej. agotamiento).</summary>
public sealed record ConditionDefinition(string Key, string Label, int? MaxLevel = null);

/// <summary>Reglas de combate que cambian según el sistema de juego.</summary>
public interface ICombatRules
{
    IReadOnlyList<ConditionDefinition> Conditions { get; }

    /// <summary>Clave de la condición de concentración, o null si el sistema no la tiene.</summary>
    string? ConcentrationKey { get; }

    bool HasDeathSaves { get; }

    /// <summary>Fases de iniciativa del sistema; vacío si no usa fases.</summary>
    IReadOnlyList<CombatPhase> Phases { get; }

    /// <summary>Aplica daño y devuelve la vida actual y temporal resultantes.</summary>
    (int Current, int Temp) ApplyDamage(int current, int temp, int amount);

    /// <summary>Aplica curación; sin máximo (null) no hay tope.</summary>
    int ApplyHealing(int current, int? max, int amount);

    /// <summary>CD de la salvación de concentración tras el daño, o null si no aplica.</summary>
    int? ConcentrationDc(int damage);
}
