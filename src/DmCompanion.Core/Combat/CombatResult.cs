using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Combat;

/// <summary>Estado resultante de una operación de combate y los avisos que debe mostrar la UI.</summary>
public sealed record CombatResult(MapState State, IReadOnlyList<CombatNotice> Notices)
{
    public static CombatResult Of(MapState state) => new(state, []);
}
