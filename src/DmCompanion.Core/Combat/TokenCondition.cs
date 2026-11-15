namespace DmCompanion.Core.Combat;

/// <summary>Duración anclada al turno de una entrada del combate: caduca cuando se agotan los turnos.</summary>
public sealed record ConditionDuration(string EntryId, int TurnsLeft);

/// <summary>Condición de un token. Key identifica una condición del SRD; sin Key es personalizada y usa Label.</summary>
public sealed record TokenCondition(string Id, string? Key, string Label, int? Level, ConditionDuration? Duration, bool Expired);

/// <summary>Salvaciones de muerte (0 a 3 de cada tipo).</summary>
public sealed record DeathSaves(int Successes, int Failures)
{
    public static DeathSaves None { get; } = new(0, 0);
}
