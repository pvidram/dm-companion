using System.Text.Json.Serialization;
using DmCompanion.Core.Combat;

namespace DmCompanion.Core.Maps;

public sealed record MapState(
    int Version,
    GridSettings Grid,
    IReadOnlyList<MapToken> Tokens,
    IReadOnlyList<MapStroke> Strokes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CombatState? Combat = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CombatSnapshot? LastCombat = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MapFogZone>? Fog = null)
{
    /// <summary>Versión que lee esta app. Se escribe 4 solo si hay alguna zona de PNJ, 3 con niebla total únicamente y 2 sin niebla, para que una app anterior abra editable lo que pueda entender.</summary>
    public const int CurrentVersion = 4;

    [JsonIgnore]
    public IReadOnlyList<MapFogZone> FogZones => Fog ?? [];

    public static MapState Empty { get; } = new(CurrentVersion, GridSettings.Default, [], []);
}
