using DmCompanion.Core.Maps;
using DmCompanion.UI.Components;

namespace DmCompanion.Screens.Services;

/// <summary>Correspondencia biyectiva entre el bando guardado en el mapa y el del design system.</summary>
public static class TokenSideMapping
{
    public static Faction ToFaction(TokenSide side) => side switch
    {
        TokenSide.Pc => Faction.Pc,
        TokenSide.Ally => Faction.Ally,
        TokenSide.Enemy => Faction.Enemy,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
    };

    public static TokenSide ToSide(Faction faction) => faction switch
    {
        Faction.Pc => TokenSide.Pc,
        Faction.Ally => TokenSide.Ally,
        Faction.Enemy => TokenSide.Enemy,
        _ => throw new ArgumentOutOfRangeException(nameof(faction), faction, null),
    };
}
