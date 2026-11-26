namespace DmCompanion.Desktop.Window;

/// <summary>Posición y tamaño de la ventana en píxeles de pantalla, y si estaba maximizada.</summary>
public sealed record WindowBounds(int X, int Y, int Width, int Height, bool Maximized);
