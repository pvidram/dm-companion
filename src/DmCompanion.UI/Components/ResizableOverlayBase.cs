using DmCompanion.UI.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DmCompanion.UI.Components;

/// <summary>
/// Base de los marcos que se redimensionan (y, según el componente, se mueven) con window-frame.js.
/// Se engancha en el primer pintado abierto, se actualiza cuando cambian tamaño, anclaje o posición, y se suelta al desmontar.
/// </summary>
public abstract class ResizableOverlayBase : OverlayBase, IWindowFrameListener
{
    [Inject] private IWindowFrameInterop Frames { get; set; } = default!;

    /// <summary>El marco se puede redimensionar (tiradores visibles). En Parchment solo cuenta si el panel no es Full.</summary>
    [Parameter] public bool Resizable { get; set; }
    /// <summary>Tamaño recordado que se aplica al marco; null deja el de la hoja de estilos.</summary>
    [Parameter] public WindowSize? FrameSize { get; set; }
    /// <summary>Esquina y distancias recordadas; solo para marcos movibles.</summary>
    [Parameter] public WindowAnchor? Anchor { get; set; }
    /// <summary>Se suelta un arrastre o redimensión: llega la geometría final.</summary>
    [Parameter] public EventCallback<WindowFrameChange> OnFrameChanged { get; set; }
    /// <summary>Doble toque en un tirador: el llamador olvida el tamaño recordado.</summary>
    [Parameter] public EventCallback OnFrameReset { get; set; }

    protected abstract WindowFrameOptions FrameOptions { get; }
    /// <summary>Hay algo que gestionar en el marco (se mueve o se redimensiona).</summary>
    protected virtual bool FrameActive => Resizable || FrameOptions.Movable;

    /// <summary>Otro dato que cambia el style inline que pinta Blazor (p. ej. el z-index): al cambiar se reaplica la geometría de JS.</summary>
    protected virtual object? StyleKey => null;

    private (WindowFrameOptions Options, object? Style)? _attached;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (!IsOpen) return;
        if (!FrameActive)
        {
            await DetachAsync();
            return;
        }

        var options = FrameOptions;
        var key = (options, StyleKey);
        if (_attached.Equals(key)) return;
        var attach = _attached is null;
        _attached = key;
        try
        {
            if (attach) await Frames.AttachAsync(Element, options, this);
            else await Frames.UpdateAsync(Element, options);
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró: no hay marco que gestionar.
        }
    }

    public Task OnFrameChangedAsync(WindowFrameChange change) => InvokeAsync(() => OnFrameChanged.InvokeAsync(change));

    public Task OnFrameResetAsync() => InvokeAsync(() => OnFrameReset.InvokeAsync());

    private async Task DetachAsync()
    {
        if (_attached is null) return;
        _attached = null;
        try
        {
            await Frames.DetachAsync(Element);
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró: no queda marco que soltar.
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await DetachAsync();
        await base.DisposeAsync();
    }
}
