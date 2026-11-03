namespace DmCompanion.UI.Components;

/// <summary>Cada bando tiene color, forma de anillo e icono propios: nunca solo color (daltonismo rojo-verde).</summary>
public static class FactionVisuals
{
    public static IconName Icon(Faction faction) => faction switch
    {
        Faction.Pc => IconName.Shield,
        Faction.Ally => IconName.Hand,
        Faction.Enemy => IconName.Swords,
        _ => throw new ArgumentOutOfRangeException(nameof(faction), faction, null),
    };

    public static string Label(Faction faction) => faction switch
    {
        Faction.Pc => "PJ",
        Faction.Ally => "Aliado",
        Faction.Enemy => "Enemigo",
        _ => throw new ArgumentOutOfRangeException(nameof(faction), faction, null),
    };

    public static string CssModifier(Faction faction) => faction.ToString().ToLowerInvariant();
}
