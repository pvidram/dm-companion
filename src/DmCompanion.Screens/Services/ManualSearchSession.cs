using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Puente de una búsqueda con el JS: recibe los aciertos y le deja consultar la cancelación entre página y página.
/// Tras cancelar ya no reenvía aciertos, aunque el JS mande alguno que tuviera en vuelo.
/// </summary>
public sealed class ManualSearchSession(Func<ManualSearchHit, Task> onHit, CancellationToken cancellationToken)
{
    [JSInvokable("onHit")]
    public Task OnHitAsync(int page, string snippet) =>
        cancellationToken.IsCancellationRequested ? Task.CompletedTask : onHit(new ManualSearchHit(page, snippet));

    [JSInvokable("isCancelled")]
    public bool IsCancelled() => cancellationToken.IsCancellationRequested;
}
