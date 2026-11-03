using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

// Único punto de interop de las notas: los componentes nunca inyectan IJSRuntime.
public sealed class WikilinkInterop(IJSRuntime jsRuntime, ILogger<WikilinkInterop> logger)
{
    // Idempotente por elemento en el lado JS: se puede llamar tras cada render.
    public async ValueTask AttachAsync<T>(ElementReference element, DotNetObjectReference<T> target) where T : class
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("dmWikilinks.attach", element, target);
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró: no hay nada que enlazar.
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        {
            // Sin `ex`: un error de JS podría citar el DOM de la nota.
            logger.LogWarning("No se pudieron enlazar los wikilinks de la nota ({ExceptionType}).", ex.GetType().FullName);
        }
    }
}
