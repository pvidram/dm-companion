using DmCompanion.Core.Combat;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tv;

/// <summary>Único punto que decide qué ve la TV del mapa: sin tokens ocultos, con alias y con vida de enemigos solo aproximada.</summary>
public static class PlayerViewProjector
{
    public static PlayerMapView Project(
        MapDefinition map, MapState state, TvFrame frame, ICombatRules rules,
        Func<MapToken, int?> maxHp, Func<MapToken, TokenLook> look)
    {
        var visible = state.Tokens.Where(t => !t.IsHidden).ToList();
        var currentTokenId = state.Combat is { CurrentEntryId: { } entryId } combat
            ? combat.Entries.FirstOrDefault(e => e.Id == entryId)?.TokenId
            : null;

        var tokens = visible.Select(t =>
        {
            var portrait = string.IsNullOrWhiteSpace(t.Alias) ? look(t).PortraitPath : null;
            return new PlayerToken(t.Id, t.PlayerName, InitialOf(t.PlayerName), portrait, t.Side, t.Size, t.X, t.Y,
                HitPointOps.IsDown(t) && !t.IsDead, currentTokenId == t.Id, t.IsDead, ActiveConditions(t).Count);
        }).ToList();

        return new PlayerMapView(map.NotePath, map.ImagePath, map.Width, map.Height, state.Grid, frame,
            tokens, state.Strokes, ProjectCombat(state, visible, currentTokenId, rules, maxHp));
    }

    private static PlayerCombat? ProjectCombat(
        MapState state, List<MapToken> visible, string? currentTokenId, ICombatRules rules, Func<MapToken, int?> maxHp)
    {
        if (state.Combat is not { } combat) return null;
        var byId = visible.ToDictionary(t => t.Id);
        var entries = new List<PlayerStripEntry>();
        foreach (var e in combat.Entries)
        {
            if (!byId.TryGetValue(e.TokenId, out var t)) continue;
            entries.Add(new PlayerStripEntry(t.Id, t.PlayerName, e.Initiative, currentTokenId == t.Id, t.IsDead,
                HpText(t, rules, maxHp(t)), ActiveConditions(t).Select(c => c.Label).ToList()));
        }
        return new PlayerCombat(combat.Round, entries);
    }

    private static List<TokenCondition> ActiveConditions(MapToken t) => t.ConditionList.Where(c => !c.Expired).ToList();

    private static string InitialOf(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();

    private static string? HpText(MapToken t, ICombatRules rules, int? max)
    {
        var current = t.HpCurrent ?? max;
        if (current is not { } hp) return null;
        if (t.Side != TokenSide.Pc) return rules.HealthLabel(hp, max);
        var text = max is { } m ? $"{hp}/{m}" : $"{hp}";
        return t.TempHp > 0 ? $"{text} (+{t.TempHp})" : text;
    }
}
