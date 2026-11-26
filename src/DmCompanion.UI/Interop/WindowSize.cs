namespace DmCompanion.UI.Interop;

/// <summary>Tamaño por eje; un eje null es "no elegido" y manda el de la hoja de estilos. Como mínimo (MinSize) ambos ejes llevan valor.</summary>
public sealed record WindowSize(double? Width, double? Height);
