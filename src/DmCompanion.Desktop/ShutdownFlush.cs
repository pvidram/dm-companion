using DmCompanion.Screens.Services;

namespace DmCompanion.Desktop;

public static class ShutdownFlush
{
    /// <summary>
    /// Guarda el mapa abierto bloqueando el hilo, con un tope. Un manejador de señal del SO es síncrono y
    /// volver de él termina el proceso, así que aquí no se puede devolver una Task ni hacer await.
    /// </summary>
    public static void FlushBlocking(MapSession session, TimeSpan timeout, ILogger logger)
    {
        try
        {
            session.FlushAsync().WaitAsync(timeout).GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException ex)
        {
            logger.LogDebug("No se guarda el mapa al cerrar: la sesión ya estaba liberada ({Error}).", ex.GetType().Name);
            // La sesión ya se liberó: no queda nada pendiente que guardar.
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning("El guardado del mapa tardó más de {Seconds} s al cerrar ({Error}).", timeout.TotalSeconds, ex.GetType().Name);
        }
    }
}
