using System.Globalization;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

/// <summary>Textos del historial de combate: cortos, en español y con el nombre del token. Solo para la UI; nunca van a los registros (logs).</summary>
public static class CombatLogText
{
    private const string UnknownToken = "Token";

    public static string Damage(MapState after, string tokenId, int amount, int? max) =>
        $"{NameOf(after, tokenId)}: −{amount} HP{HpSuffix(after, tokenId, max)}";

    public static string Heal(MapState before, MapState after, string tokenId, int amount, int? max) =>
        $"{NameOf(after, tokenId)}: +{ActualDelta(before, after, tokenId, max) ?? amount} HP{HpSuffix(after, tokenId, max)}";

    public static string SetHp(MapState before, MapState after, string tokenId, int? max)
    {
        var delta = ActualDelta(before, after, tokenId, max) ?? 0;
        var sign = delta < 0 ? "−" : "+";
        return $"{NameOf(after, tokenId)}: {sign}{Math.Abs(delta)} HP{HpSuffix(after, tokenId, max)}";
    }

    public static string TempHp(MapState after, string tokenId, int value) =>
        value > 0 ? $"{NameOf(after, tokenId)}: {value} HP temporales" : $"{NameOf(after, tokenId)}: sin HP temporales";

    public static string DeathSaves(MapState after, string tokenId) =>
        after.Tokens.FirstOrDefault(t => t.Id == tokenId)?.DeathSaves is { } d
            ? $"{NameOf(after, tokenId)}: salvaciones de muerte ({Count(d.Successes, "éxito", "éxitos")}, {Count(d.Failures, "fallo", "fallos")})"
            : $"{NameOf(after, tokenId)}: salvaciones de muerte";

    public static string Dead(MapState s, string tokenId) => $"{NameOf(s, tokenId)}: muerto";
    public static string Revive(MapState s, string tokenId) => $"{NameOf(s, tokenId)}: revive";

    public static string ConditionAdded(MapState after, string tokenId, ConditionDraft draft) =>
        $"{NameOf(after, tokenId)}: {draft.Label}{RoundsSuffix(draft.Rounds)}";

    public static string ConditionEdited(MapState after, string tokenId, ConditionDraft draft) =>
        $"{NameOf(after, tokenId)}: {draft.Label} editado{RoundsSuffix(draft.Rounds)}";

    public static string ConditionRemoved(MapState before, string tokenId, string conditionId) =>
        $"{NameOf(before, tokenId)}: quita {ConditionLabel(before, tokenId, conditionId)}";

    public static string ConditionKept(MapState before, string tokenId, string conditionId, int? rounds) =>
        $"{NameOf(before, tokenId)}: mantiene {ConditionLabel(before, tokenId, conditionId)}{RoundsSuffix(rounds)}";

    /// <summary>«R3 · Turno de Bruno», con los estados que se han acabado por el camino («· se acaba Asustado en Goblin 2»).</summary>
    public static string Turn(MapState before, MapState after, bool byHand = false)
    {
        var text = $"R{after.Combat?.Round} · Turno de {EntryName(after, after.Combat?.CurrentEntryId)}{(byHand ? " (a mano)" : string.Empty)}";
        foreach (var (token, condition) in NewlyExpired(before, after))
            text += $" · se acaba {condition.Label} en {token.Name}";
        return text;
    }

    public static string EntryAdded(MapState after, string tokenId) => $"{NameOf(after, tokenId)} entra en el combate";

    public static string EntryRemoved(MapState before, string entryId) => $"{EntryName(before, entryId)} sale del combate";

    public static string EntryMoved(MapState before, string entryId) => $"Reordena: {EntryName(before, entryId)}";

    private static IEnumerable<(MapToken Token, TokenCondition Condition)> NewlyExpired(MapState before, MapState after)
    {
        foreach (var token in after.Tokens)
        {
            var old = before.Tokens.FirstOrDefault(t => t.Id == token.Id)?.ConditionList ?? [];
            foreach (var condition in token.ConditionList.Where(c => c.Expired && old.Any(o => o.Id == c.Id && !o.Expired)))
                yield return (token, condition);
        }
    }

    private static string NameOf(MapState s, string tokenId) =>
        s.Tokens.FirstOrDefault(t => t.Id == tokenId)?.Name ?? UnknownToken;

    private static string EntryName(MapState s, string? entryId) =>
        s.Combat?.Entries.FirstOrDefault(e => e.Id == entryId) is { } entry ? NameOf(s, entry.TokenId) : UnknownToken;

    private static string ConditionLabel(MapState s, string tokenId, string conditionId) =>
        s.Tokens.FirstOrDefault(t => t.Id == tokenId)?.ConditionList.FirstOrDefault(c => c.Id == conditionId)?.Label ?? "estado";

    private static int? ActualDelta(MapState before, MapState after, string tokenId, int? max)
    {
        var old = before.Tokens.FirstOrDefault(t => t.Id == tokenId)?.HpCurrent ?? max;
        var now = after.Tokens.FirstOrDefault(t => t.Id == tokenId)?.HpCurrent;
        return old is { } o && now is { } n ? n - o : null;
    }

    private static string HpSuffix(MapState s, string tokenId, int? max)
    {
        var current = s.Tokens.FirstOrDefault(t => t.Id == tokenId)?.HpCurrent ?? max;
        if (current is not { } c) return string.Empty;
        return max is { } m
            ? string.Create(CultureInfo.InvariantCulture, $" ({c}/{m})")
            : string.Create(CultureInfo.InvariantCulture, $" ({c})");
    }

    private static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n.ToString(CultureInfo.InvariantCulture)} {many}";

    private static string RoundsSuffix(int? rounds) => rounds switch
    {
        null => string.Empty,
        1 => " (1 ronda)",
        var n => string.Create(CultureInfo.InvariantCulture, $" ({n} rondas)"),
    };
}
