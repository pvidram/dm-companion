using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Único punto de interop de la vista de jugadores (`window.playerPreview`): los componentes nunca inyectan IJSRuntime.
/// Scoped, como el lienzo del mapa.
/// </summary>
public sealed class PlayerPreviewInterop(IJSRuntime js, ILogger<PlayerPreviewInterop> logger)
{
    public Task ShowAsync(ElementReference host, string json) => TryInvokeAsync("playerPreview.show", host, json);

    public Task UpdateAsync(string json) => TryInvokeAsync("playerPreview.update", json);

    public Task HideAsync() => TryInvokeAsync("playerPreview.hide");

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
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        {
            logger.LogWarning("Falló la llamada {Identifier} de la vista de jugadores ({ExceptionType}).", identifier, ex.GetType().FullName);
        }
    }
}
