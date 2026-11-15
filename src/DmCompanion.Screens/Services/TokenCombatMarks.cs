using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

/// <summary>Lo que el lienzo pinta del combate sobre un token: 0 HP, muerto, turno actual, selección y estados.</summary>
public sealed record TokenCombatMarks(bool Down, bool Current, bool Selected, int Conditions, bool Expired, bool Dead = false)
{
    public static TokenCombatMarks For(MapState state, MapToken token, string? selectedTokenId)
    {
        var currentTokenId = state.Combat is { CurrentEntryId: { } entryId } combat
            ? combat.Entries.FirstOrDefault(e => e.Id == entryId)?.TokenId
            : null;
        var conditions = token.ConditionList;
        return new(
            HitPointOps.IsDown(token),
            currentTokenId == token.Id,
            state.Combat is not null && selectedTokenId == token.Id,
            conditions.Count,
            conditions.Any(c => c.Expired),
            token.IsDead);
    }
}
