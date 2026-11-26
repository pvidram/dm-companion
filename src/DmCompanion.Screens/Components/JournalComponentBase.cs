using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Components;

/// <summary>
/// Base de los componentes del diario: además de los cambios del vault y del shell, se repinta cuando el índice relista
/// imágenes. Los derivados que sobrescriban OnInitialized deben llamar a base.OnInitialized().
/// </summary>
public abstract class JournalComponentBase : DmComponentBase
{
    [Inject] protected JournalIndex Journal { get; set; } = default!;
    [Inject] private ILogger<JournalComponentBase> JournalLogger { get; set; } = default!;

    /// <summary>
    /// Los componentes del diario que cuelgan de pantallas existentes (Jugadores, ficha, glosario) solo se pintan si el índice
    /// está registrado, como ya hacen <c>TvNowShowing</c> y Diagnóstico con lo suyo: así los tests y hosts sin diario no cambian.
    /// </summary>
    public static bool IsAvailable(IServiceProvider services) => services.GetService<JournalIndex>() is not null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Journal.Changed += OnJournalChanged;
    }

    // Llega desde hilos del watcher o del listado: hay que volver al dispatcher de Blazor.
    private void OnJournalChanged(object? sender, EventArgs e) => _ = RefreshJournalAsync();

    private async Task RefreshJournalAsync()
    {
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
            // El componente ya se retiró mientras llegaba el evento.
        }
        catch (Exception ex)
        {
            JournalLogger.LogError("Fallo al refrescar {Component} tras relistar las imágenes del diario ({ExceptionType}).", GetType().Name, ex.GetType().FullName);
        }
    }

    public override void Dispose()
    {
        Journal.Changed -= OnJournalChanged;
        base.Dispose();
    }
}
