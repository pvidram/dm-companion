namespace DmCompanion.Screens.Components;

/// <summary>Toque en una casilla de la tira retraída: su posición y el punto del toque en coordenadas del viewport.</summary>
public sealed record StripTap(int Index, double ClientX, double ClientY);
