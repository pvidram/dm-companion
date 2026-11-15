namespace DmCompanion.Core.Combat;

/// <summary>Datos para crear o editar un estado. Rounds es el número de rondas; AnchorEntryId, la entrada cuyo turno las descuenta.</summary>
public sealed record ConditionDraft(string? Key, string Label, int? Level, int? Rounds, string? AnchorEntryId);
