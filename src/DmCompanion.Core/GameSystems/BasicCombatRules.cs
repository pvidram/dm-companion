using DmCompanion.Core.Combat;

namespace DmCompanion.Core.GameSystems;

/// <summary>Reglas mínimas para sistemas sin reglas de combate propias: sin catálogo, sin vida temporal ni concentración.</summary>
public sealed class BasicCombatRules : ICombatRules
{
    public static BasicCombatRules Instance { get; } = new();

    private BasicCombatRules() { }

    public IReadOnlyList<ConditionDefinition> Conditions => [];
    public string? ConcentrationKey => null;
    public bool HasDeathSaves => false;
    public IReadOnlyList<CombatPhase> Phases => [];

    public (int Current, int Temp) ApplyDamage(int current, int temp, int amount) =>
        amount <= 0 ? (current, temp) : (Math.Max(0, current - amount), temp);

    public int ApplyHealing(int current, int? max, int amount)
    {
        if (amount <= 0) return current;
        var healed = current + amount;
        return max is { } cap ? Math.Min(healed, cap) : healed;
    }

    public int? ConcentrationDc(int damage) => null;

    public string? HealthLabel(int current, int? max) => HealthLabels.For(current, max);
}
