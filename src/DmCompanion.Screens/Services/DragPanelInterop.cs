using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Único punto de interop del arrastre de paneles flotantes (`window.dragPanel`): los componentes nunca inyectan IJSRuntime.
/// Scoped, como el lienzo del mapa.
/// </summary>
public sealed class DragPanelInterop(IJSRuntime js, ILogger<DragPanelInterop> logger)
{
    /// <summary>Registra el arrastre por la cabecera del panel (una sola vez) y lo coloca en (x, y), px relativos a su contenedor.</summary>
    public async Task AttachAsync(ElementReference panel, double x, double y) => await TryInvokeAsync("dragPanel.attach", panel, x, y);

    private async Task TryInvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró.
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning("Falló la llamada {Identifier} al arrastre de paneles ({ExceptionType}).", identifier, ex.GetType().FullName);
        }
    }
}
