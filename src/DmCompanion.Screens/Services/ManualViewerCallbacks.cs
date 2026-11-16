using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>Avisos del visor JS hacia .NET mientras hay un documento abierto.</summary>
public sealed class ManualViewerCallbacks(Action<int> onPageTurn)
{
    /// <summary>Deslizamiento horizontal: 1 pide la página siguiente y -1 la anterior; el componente decide.</summary>
    [JSInvokable("onSwipe")]
    public void OnSwipe(int direction) => onPageTurn(direction);
}
