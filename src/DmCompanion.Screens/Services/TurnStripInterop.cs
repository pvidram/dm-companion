using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>Una fila de la tira se soltó en otra posición: <c>NewIndex</c> es el lugar que ocupará en la lista final.</summary>
public sealed record EntryMovedEventArgs(string EntryId, int NewIndex);

/// <summary>
/// Único punto de interop del arrastre de la tira de turnos (`window.turnStrip`): los componentes nunca inyectan IJSRuntime.
/// Scoped, como el lienzo del mapa.
/// </summary>
public sealed class TurnStripInterop(IJSRuntime js, ILogger<TurnStripInterop> logger) : IAsyncDisposable
{
    private DotNetObjectReference<TurnStripInterop>? _self;
    private bool _attached;

    public event EventHandler<EntryMovedEventArgs>? EntryMoved;

    public async Task AttachAsync(ElementReference list)
    {
        _self ??= DotNetObjectReference.Create(this);
        _attached = await TryInvokeAsync("turnStrip.attach", list, _self);
    }

    public async Task DetachAsync()
    {
        if (!_attached) return;
        _attached = false;
        await TryInvokeAsync("turnStrip.detach");
    }

    // Lo que llega de JS se valida igual: acaba en el `.state.json`.
    [JSInvokable]
    public void OnEntryMoved(string entryId, int newIndex)
    {
        if (string.IsNullOrEmpty(entryId) || newIndex < 0) return;
        EntryMoved?.Invoke(this, new EntryMovedEventArgs(entryId, newIndex));
    }

    public async ValueTask DisposeAsync()
    {
        await DetachAsync();
        _self?.Dispose();
    }

    private async Task<bool> TryInvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false; // El circuito ya se cerró.
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        {
            logger.LogWarning("Falló la llamada {Identifier} a la tira de turnos ({ExceptionType}).", identifier, ex.GetType().FullName);
            return false;
        }
    }
}
