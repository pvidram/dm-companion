namespace DmCompanion.Core.Maps;

/// <summary>Trazo a mano alzada. Points es una lista plana de tríos x, y, presión.</summary>
public sealed record MapStroke(string Id, string Color, double Width, IReadOnlyList<double> Points);
