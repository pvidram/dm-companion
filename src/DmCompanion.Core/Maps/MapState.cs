namespace DmCompanion.Core.Maps;

public sealed record MapState(
    int Version,
    GridSettings Grid,
    IReadOnlyList<MapToken> Tokens,
    IReadOnlyList<MapStroke> Strokes)
{
    public const int CurrentVersion = 1;

    public static MapState Empty { get; } = new(CurrentVersion, GridSettings.Default, [], []);
}
