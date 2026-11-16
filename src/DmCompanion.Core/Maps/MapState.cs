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
    /// <summary>Versión que lee esta app. Se escribe 3 solo si el mapa tiene niebla; sin ella se sigue escribiendo 2 para que una app anterior lo abra editable.</summary>
    public const int CurrentVersion = 3;

    [JsonIgnore]
    public IReadOnlyList<MapFogZone> FogZones => Fog ?? [];

    public static MapState Empty { get; } = new(CurrentVersion, GridSettings.Default, [], []);
}
