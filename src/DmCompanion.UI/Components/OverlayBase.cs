using DmCompanion.UI.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DmCompanion.UI.Components;

/// <summary>
/// Base de lo que se superpone al mapa: entra y sale del foco al abrirse y cerrarse, y Esc lo cierra.
/// Cerrado sigue montado (atributo hidden) para no perder el estado del contenido.
/// </summary>
public abstract class OverlayBase : ComponentBase, IAsyncDisposable
{
    [Inject] private IFocusInterop Focus { get; set; } = default!;

    /// <summary>Abierto (visible y con el foco dentro) o cerrado pero montado. Parchment y QuickCard nacen abiertos; Dialog, cerrado.</summary>
    [Parameter] public bool IsOpen { get; set; } = true;
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Atributos HTML adicionales para el elemento principal (en Dialog, el que lleva role="dialog"). Los que fija el componente prevalecen; <c>class</c> se añade a las clases propias.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    protected ElementReference Element { get; set; }
    protected virtual bool TrapFocus => false;

    private bool _focusEntered;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen && !_focusEntered)
        {
            _focusEntered = true;
            await Focus.EnterAsync(Element, TrapFocus);
        }
        else if (!IsOpen && _focusEntered)
        {
            _focusEntered = false;
            await Focus.ExitAsync(Element);
        }
    }

    protected async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && OnClose.HasDelegate) await OnClose.InvokeAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (!_focusEntered) return;
        _focusEntered = false;
        try
        {
            await Focus.ExitAsync(Element);
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró: no hay navegador al que devolver el foco.
        }
        GC.SuppressFinalize(this);
    }
}
