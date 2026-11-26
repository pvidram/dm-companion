using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>Lanza eventos aislando el fallo de cada suscriptor: uno que lance no impide avisar a los demás.</summary>
public static class SafeEvents
{
    public static void Raise(EventHandler? handler, object sender, EventArgs args, ILogger logger, string eventName)
    {
        if (handler is null) return;
        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler)subscriber)(sender, args);
            }
            catch (Exception ex)
            {
                // Un suscriptor que falla no debe tumbar a los demás ni al emisor; solo el tipo, nunca el mensaje.
                logger.LogError("Un suscriptor de {EventName} ha lanzado una excepción ({ExceptionType})", eventName, ex.GetType().FullName);
            }
        }
    }
}
