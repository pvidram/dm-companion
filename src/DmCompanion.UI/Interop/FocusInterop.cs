using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DmCompanion.UI.Interop;

public sealed class FocusInterop(IJSRuntime jsRuntime) : IFocusInterop, IAsyncDisposable
{
    public const string ModulePath = "./_content/DmCompanion.UI/focus-trap.js";

    private Task<IJSObjectReference>? _module;

    // Si la importación falla, no se cachea el fallo: la siguiente llamada vuelve a intentarlo.
    private async Task<IJSObjectReference> LoadModuleAsync()
    {
        var pending = _module ??= jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
        try
        {
            return await pending;
        }
        catch
        {
            if (ReferenceEquals(_module, pending)) _module = null;
            throw;
        }
    }

    public async ValueTask EnterAsync(ElementReference container, bool trap) =>
        await (await LoadModuleAsync()).InvokeVoidAsync("enter", container, trap);

    public async ValueTask ExitAsync(ElementReference container) =>
        await (await LoadModuleAsync()).InvokeVoidAsync("exit", container);

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try
        {
            await (await _module).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró: el módulo JS desapareció con él.
        }
    }
}
