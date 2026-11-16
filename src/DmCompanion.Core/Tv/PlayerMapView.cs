using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tv;

/// <summary>Aspecto de un token en la TV: retrato opcional (la inicial la calcula el proyector).</summary>
public sealed record TokenLook(string? PortraitPath);

public sealed record PlayerToken(
    string Id, string Name, string Initial, string? PortraitPath, TokenSide Side, int Size,
    double X, double Y, bool Down, bool Current, bool Dead, int Conditions);

public sealed record PlayerStripEntry(
    string TokenId, string Name, int? Initiative, bool Current, bool Dead, string? Hp, IReadOnlyList<string> Conditions);

public sealed record PlayerCombat(int Round, IReadOnlyList<PlayerStripEntry> Entries);

/// <summary>Lo único que ven los jugadores del mapa. <c>Fog</c>: puntos de las zonas de niebla tapadas, en el orden del estado.</summary>
public sealed record PlayerMapView(
    string MapPath, string? ImagePath, double Width, double Height, GridSettings Grid, TvFrame Frame,
    IReadOnlyList<PlayerToken> Tokens, IReadOnlyList<MapStroke> Strokes, PlayerCombat? Combat,
    IReadOnlyList<IReadOnlyList<double>> Fog);
