using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>Avisos del visor JS hacia .NET mientras hay un documento abierto.</summary>
public sealed class ManualViewerCallbacks(Action<int> onPageChanged, Action<string> onPageFailed)
{
    /// <summary>La página que se está leyendo cambió al desplazarse (el JS lo avisa con retardo y solo si cambia).</summary>
    [JSInvokable("onPageChanged")]
    public void OnPageChanged(int page) => onPageChanged(page);

    /// <summary>Una página no se pudo pintar ni con el reintento a menor escala; llega solo el tipo de error.</summary>
    [JSInvokable("onPageFailed")]
    public void OnPageFailed(string errorType) => onPageFailed(errorType);
}
