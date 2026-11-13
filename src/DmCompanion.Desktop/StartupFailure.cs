namespace DmCompanion.Desktop;

/// <summary>Un fallo de arranque se muestra en la consola y se registra solo con el tipo de la excepción.</summary>
public static class StartupFailure
{
    /// <summary>
    /// Escribe el mensaje, registra el tipo de la excepción y, si <paramref name="wait"/>, espera a Intro para que la ventana
    /// no se cierre sin rastro. Devuelve el código de salida (1).
    /// </summary>
    public static int Report(string message, ILogger? logger, Exception? exception, bool wait, TextWriter output, TextReader input)
    {
        output.WriteLine(message);
        if (logger is not null)
        {
            if (exception is null) logger.LogError("{Message}", message);
            else logger.LogError(exception, "El arranque falló.");
        }
        if (wait)
        {
            output.WriteLine("Pulsa Intro para cerrar esta ventana.");
            input.ReadLine();
        }
        return 1;
    }

    public static string UnexpectedFailureMessage(bool hasStarted) => hasStarted
        ? "DM Companion se ha detenido por un error. Ábrelo de nuevo; si se repite, revisa los registros en %LOCALAPPDATA%\\DmCompanion\\logs."
        : "DM Companion no ha podido arrancar. Cierra otras copias abiertas e inténtalo de nuevo; si se repite, revisa los registros en %LOCALAPPDATA%\\DmCompanion\\logs.";

    /// <summary>Por defecto solo espera con una consola interactiva; <c>Desktop:WaitOnError</c> lo fuerza o lo desactiva.</summary>
    public static bool ShouldWait(IConfiguration config) => config.GetValue("Desktop:WaitOnError", !Console.IsInputRedirected);
}
