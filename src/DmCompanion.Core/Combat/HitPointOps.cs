using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Combat;

/// <summary>Puntos de golpe, HP temporales y salvaciones de muerte. Funciones puras; si nada cambia devuelven el mismo estado.</summary>
public static class HitPointOps
{
    public static bool IsDown(MapToken t) => t.HpCurrent == 0;

    public static CombatResult Damage(MapState s, string tokenId, int amount, int? max, ICombatRules rules)
    {
        if (amount <= 0 || Find(s, tokenId) is not { } token) return CombatResult.Of(s);
        if ((token.HpCurrent ?? max) is not { } current) return CombatResult.Of(s);

        var (newCurrent, newTemp) = rules.ApplyDamage(current, token.TempHp, amount);
        var changed = newCurrent != current || newTemp != token.TempHp;
        var next = Update(s, tokenId, t => t with { HpCurrent = newCurrent, HpTemp = newTemp > 0 ? newTemp : null });
        return new CombatResult(next, changed ? ConcentrationNotices(token, amount, rules) : []);
    }

    public static MapState Heal(MapState s, string tokenId, int amount, int? max, ICombatRules rules)
    {
        if (amount <= 0 || Find(s, tokenId) is not { } token) return s;
        if ((token.HpCurrent ?? max) is not { } current) return s;

        var healed = rules.ApplyHealing(current, max, amount);
        var revived = current == 0 && healed > 0;
        return Update(s, tokenId, t => t with { HpCurrent = healed, DeathSaves = revived ? null : t.DeathSaves });
    }

    public static MapState SetTempHp(MapState s, string tokenId, int value) =>
        Update(s, tokenId, t => t with { HpTemp = value > 0 ? value : null });

    /// <summary>Guarda los HP actuales recortados a [0, max]. Al subir de 0 se ponen a cero las salvaciones de muerte (igual que <c>MapEditor.SetHp</c>).</summary>
    public static MapState SetHp(MapState s, string tokenId, int value, int max) =>
        Update(s, tokenId, t =>
        {
            var hp = Math.Clamp(value, 0, Math.Max(max, 0));
            return t with { HpCurrent = hp, DeathSaves = t.HpCurrent == 0 && hp > 0 ? null : t.DeathSaves };
        });

    public static MapState SetDeathSaves(MapState s, string tokenId, int successes, int failures) =>
        Update(s, tokenId, t => t with { DeathSaves = new DeathSaves(Math.Clamp(successes, 0, 3), Math.Clamp(failures, 0, 3)) });

    private static IReadOnlyList<CombatNotice> ConcentrationNotices(MapToken token, int damage, ICombatRules rules)
    {
        if (rules.ConcentrationKey is not { } key) return [];
        if (!token.ConditionList.Any(c => c.Key == key && !c.Expired)) return [];
        return rules.ConcentrationDc(damage) is { } dc ? [new ConcentrationCheck(token.Id, dc)] : [];
    }

    private static MapToken? Find(MapState s, string tokenId) => s.Tokens.FirstOrDefault(t => t.Id == tokenId);

    private static MapState Update(MapState s, string tokenId, Func<MapToken, MapToken> change) =>
        Find(s, tokenId) is null ? s : s with { Tokens = s.Tokens.Select(t => t.Id == tokenId ? change(t) : t).ToList() };
}
