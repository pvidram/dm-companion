using DmCompanion.Core.Vault;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Components;

// Los componentes derivados que sobrescriban OnInitialized deben llamar a base.OnInitialized().
public abstract class DmComponentBase : ComponentBase, IDisposable
{
    [Inject] protected VaultHost Host { get; set; } = default!;
    [Inject] protected ShellState Shell { get; set; } = default!;
    [Inject] private ILogger<DmComponentBase> Logger { get; set; } = default!;

    protected VaultIndex Index => Host.Current?.Index ?? VaultIndex.Empty;

    protected override void OnInitialized()
    {
        Host.Changed += OnStateChanged;
        Shell.Changed += OnStateChanged;
    }

    protected virtual void OnVaultChanged() { }

    public virtual void Dispose()
    {
        Host.Changed -= OnStateChanged;
        Shell.Changed -= OnStateChanged;
        GC.SuppressFinalize(this);
    }

    // Los eventos llegan desde hilos del watcher: hay que volver al dispatcher de Blazor.
    // La tarea de InvokeAsync se observa: un fallo en OnVaultChanged se registra, nunca se descarta.
    private void OnStateChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            await InvokeAsync(() =>
            {
                OnVaultChanged();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            // Sin `ex`: al renderizar una nota el mensaje podría citar su texto.
            Logger.LogError("Fallo al refrescar {Component} tras un cambio de estado ({ExceptionType}).", GetType().Name, ex.GetType().FullName);
        }
    }
}
