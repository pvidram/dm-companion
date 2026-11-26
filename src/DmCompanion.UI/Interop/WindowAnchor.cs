namespace DmCompanion.UI.Interop;

/// <summary>Esquina del área más cercana al marco y distancias del borde del marco al borde del área en esa esquina.</summary>
public sealed record WindowAnchor(WindowCorner Corner, double OffsetX, double OffsetY);
