using DmCompanion.UI.Interop;

namespace DmCompanion.Screens.Services;

/// <summary>Tarjeta de criatura abierta como ventana flotante. <c>Order</c> crece con cada apertura o traída al frente.</summary>
public sealed record TokenCardWindow(string TokenId, double X, double Y, WindowSize? Size, bool IsPinned, bool IsEditing, long Order);
