using DmCompanion.Core.Combat;

namespace DmCompanion.Core.GameSystems;

/// <summary>Reglas de combate de D&amp;D 5e (2024): condiciones del SRD, vida temporal, salvaciones de muerte y concentración.</summary>
public sealed class Dnd5eCombatRules : ICombatRules
{
    public static Dnd5eCombatRules Instance { get; } = new();

    private Dnd5eCombatRules() { }

    public IReadOnlyList<ConditionDefinition> Conditions { get; } =
    [
        new("grappled", "Agarrado"), new("restrained", "Apresado"), new("frightened", "Asustado"),
        new("stunned", "Aturdido"), new("blinded", "Cegado"), new("prone", "Derribado"),
        new("deafened", "Ensordecido"), new("poisoned", "Envenenado"), new("charmed", "Hechizado"),
        new("incapacitated", "Incapacitado"), new("unconscious", "Inconsciente"), new("invisible", "Invisible"),
        new("paralyzed", "Paralizado"), new("petrified", "Petrificado"),
        new("exhaustion", "Agotamiento", 6), new("concentration", "Concentración"),
    ];

    public string? ConcentrationKey => "concentration";
    public bool HasDeathSaves => true;
    public IReadOnlyList<CombatPhase> Phases => [];

    public (int Current, int Temp) ApplyDamage(int current, int temp, int amount)
    {
        if (amount <= 0) return (current, temp);
        var absorbed = Math.Min(temp, amount);
        return (Math.Max(0, current - (amount - absorbed)), temp - absorbed);
    }

    public int ApplyHealing(int current, int? max, int amount)
    {
        if (amount <= 0) return current;
        var healed = current + amount;
        return max is { } cap ? Math.Min(healed, cap) : healed;
    }

    public int? ConcentrationDc(int damage) => damage <= 0 ? null : Math.Min(30, Math.Max(10, damage / 2));
}
