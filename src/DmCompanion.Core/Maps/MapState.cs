using System.Text.Json.Serialization;
using DmCompanion.Core.Combat;

namespace DmCompanion.Core.Maps;

public sealed record MapState(
    int Version,
    GridSettings Grid,
    IReadOnlyList<MapToken> Tokens,
    IReadOnlyList<MapStroke> Strokes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CombatState? Combat = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CombatSnapshot? LastCombat = null)
{
    public const int CurrentVersion = 2;

    public static MapState Empty { get; } = new(CurrentVersion, GridSettings.Default, [], []);
}
