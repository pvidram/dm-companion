namespace DmCompanion.UI.Interop;

/// <summary>Size es null cuando el gesto solo movió el marco (no cambió su tamaño); si no, los ejes que el usuario ha elegido (null = no elegido).
/// Anchor es null en marcos no movibles (solo cambian de tamaño).</summary>
public sealed record WindowFrameChange(double X, double Y, WindowSize? Size, WindowAnchor? Anchor);
