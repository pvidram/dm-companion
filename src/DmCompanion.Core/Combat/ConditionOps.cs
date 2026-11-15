using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Combat;

/// <summary>
/// Estados de los tokens. Funciones puras sobre <see cref="MapState"/>; si nada cambia devuelven el mismo estado.
/// Solo acotan el nivel a ≥ 1: el máximo del catálogo lo aplica quien llama.
/// </summary>
public static class ConditionOps
{
    public static MapState Add(MapState s, string tokenId, ConditionDraft d, Func<string> newId)
    {
        if (Find(s, tokenId) is not { } token) return s;
        if (string.IsNullOrWhiteSpace(d.Label) && d.Key is null) return s;
        var condition = new TokenCondition(newId(), d.Key, d.Label, ClampLevel(d.Level), ResolveDuration(s, token, d.Rounds, d.AnchorEntryId), false);
        return WithConditions(s, tokenId, [.. token.ConditionList, condition]);
    }

    public static MapState Update(MapState s, string tokenId, string conditionId, ConditionDraft d)
    {
        if (Find(s, tokenId) is not { } token) return s;
        if (token.ConditionList.FirstOrDefault(c => c.Id == conditionId) is not { } current) return s;
        var updated = current with
        {
            Key = d.Key,
            Label = d.Label,
            Level = ClampLevel(d.Level),
            Duration = ResolveDuration(s, token, d.Rounds, d.AnchorEntryId),
            Expired = false,
        };
        return Replace(s, token, conditionId, updated);
    }

    public static MapState Remove(MapState s, string tokenId, string conditionId)
    {
        if (Find(s, tokenId) is not { } token) return s;
        if (token.ConditionList.All(c => c.Id != conditionId)) return s;
        return WithConditions(s, tokenId, token.ConditionList.Where(c => c.Id != conditionId).ToList());
    }

    /// <summary>Mantiene un estado vencido: sin rondas lo deja sin duración; con rondas reinicia la cuenta en la misma ancla.</summary>
    public static MapState KeepExpired(MapState s, string tokenId, string conditionId, int? rounds)
    {
        if (Find(s, tokenId) is not { } token) return s;
        if (token.ConditionList.FirstOrDefault(c => c.Id == conditionId) is not { } current) return s;
        var anchor = current.Duration?.EntryId;
        return Replace(s, token, conditionId, current with { Duration = ResolveDuration(s, token, rounds, anchor), Expired = false });
    }

    private static MapToken? Find(MapState s, string tokenId) => s.Tokens.FirstOrDefault(t => t.Id == tokenId);

    private static int? ClampLevel(int? level) => level is { } l ? Math.Max(1, l) : null;

    private static ConditionDuration? ResolveDuration(MapState s, MapToken token, int? rounds, string? anchorEntryId)
    {
        if (rounds is not > 0 || s.Combat is not { } combat) return null;
        var anchor = combat.Entries.FirstOrDefault(e => e.Id == anchorEntryId)
                     ?? combat.Entries.FirstOrDefault(e => e.TokenId == token.Id);
        return anchor is null ? null : new ConditionDuration(anchor.Id, rounds.Value);
    }

    private static MapState Replace(MapState s, MapToken token, string conditionId, TokenCondition updated) =>
        WithConditions(s, token.Id, token.ConditionList.Select(c => c.Id == conditionId ? updated : c).ToList());

    private static MapState WithConditions(MapState s, string tokenId, IReadOnlyList<TokenCondition> conditions) =>
        s with { Tokens = s.Tokens.Select(t => t.Id == tokenId ? t with { Conditions = conditions.Count == 0 ? null : conditions } : t).ToList() };
}
