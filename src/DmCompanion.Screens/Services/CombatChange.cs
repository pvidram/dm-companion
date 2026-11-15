using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

/// <summary>Compara dos estados solo en lo que entra en el historial (bloque de combate y campos de combate de los tokens), para no apuntar acciones que no cambiaron nada.</summary>
internal static class CombatChange
{
    public static bool Differs(MapState a, MapState b) => CombatDiffers(a.Combat, b.Combat) || TokensDiffer(a.Tokens, b.Tokens);

    /// <summary>PJ vivos que pasan a tener 3 fallos de salvación de muerte con esta operación.</summary>
    public static IEnumerable<string> NewThreeFailures(MapState before, MapState after) =>
        after.Tokens.Where(t => t.Side == TokenSide.Pc && !t.IsDead && t.DeathSaves is { Failures: >= 3 } &&
                                !(before.Tokens.FirstOrDefault(o => o.Id == t.Id) is { DeathSaves.Failures: >= 3 }))
            .Select(t => t.Id);

    private static bool CombatDiffers(CombatState? a, CombatState? b) =>
        a is null || b is null
            ? !(a is null && b is null)
            : a.Round != b.Round || a.CurrentEntryId != b.CurrentEntryId || !a.Entries.SequenceEqual(b.Entries);

    private static bool TokensDiffer(IReadOnlyList<MapToken> a, IReadOnlyList<MapToken> b)
    {
        if (a.Count != b.Count) return true;
        for (var i = 0; i < a.Count; i++)
        {
            var (x, y) = (a[i], b[i]);
            if (x.Id != y.Id || x.HpCurrent != y.HpCurrent || x.HpTemp != y.HpTemp || x.Dead != y.Dead || x.DeathSaves != y.DeathSaves ||
                !x.ConditionList.SequenceEqual(y.ConditionList)) return true;
        }
        return false;
    }
}
