namespace DmCompanion.Screens.Services;

/// <summary>Rectángulo de recorte en fracciones 0..1 de la página (origen arriba a la izquierda).</summary>
public sealed record CropRect(double X, double Y, double Width, double Height);
