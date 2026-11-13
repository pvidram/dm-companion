using DmCompanion.Screens.Services;

namespace DmCompanion.Desktop;

/// <summary>Al parar el host guarda el mapa abierto, con un tope de 5 s para no colgar el cierre.</summary>
public sealed class MapFlushOnStop(MapSession session, ILogger<MapFlushOnStop> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await session.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (ObjectDisposedException ex)
        {
            logger.LogDebug("No se guarda el mapa al cerrar: la sesión ya estaba liberada ({Error}).", ex.GetType().Name);
            // La sesión ya se liberó (el contenedor se desmontó antes): no queda nada pendiente que guardar.
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning("El guardado del mapa tardó más de 5 s al cerrar ({Error}).", ex.GetType().Name);
        }
    }
}
