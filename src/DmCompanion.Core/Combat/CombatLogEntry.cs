namespace DmCompanion.Core.Combat;

/// <summary>Entrada del historial: Before y After son el estado justo antes y después de la acción. Undoable es false en las acciones que no se deshacen (empezar combate).</summary>
public sealed record CombatLogEntry(
    string Id,
    int Round,
    string Text,
    CombatSnapshot? Before,
    CombatSnapshot? After,
    bool Undone,
    bool Undoable = true);
