using System.Text.Json.Serialization;

namespace DmCompanion.Core.Combat;

/// <summary>Fase de iniciativa de un participante (iniciativa por fases: rápida o lenta).</summary>
public enum CombatPhase { Fast, Slow }

/// <summary>Participante del combate: apunta a un token del mapa. Initiative es null mientras no se ha tirado.</summary>
public sealed record CombatEntry(string Id, string TokenId, int? Initiative, CombatPhase? Phase = null);

/// <summary>Combate en curso. CurrentEntryId es la entrada con el turno (null si aún no ha empezado). Log es el historial (tope 100, el más antiguo primero); se omite si no hay.</summary>
public sealed record CombatState(
    int Round,
    string? CurrentEntryId,
    IReadOnlyList<CombatEntry> Entries,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CombatLogEntry>? Log = null);
