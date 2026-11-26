namespace DmCompanion.Desktop;

/// <summary>Un fallo de arranque se muestra en un diálogo y se registra solo con el tipo de la excepción.</summary>
public static class StartupFailure
{
    /// <summary>Muestra el mensaje con <paramref name="show"/>, lo registra y devuelve el código de salida (1).</summary>
    public static int Report(string message, ILogger? logger, Exception? exception, Action<string> show)
    {
        if (logger is not null)
        {
            if (exception is null) logger.LogError("{Message}", message);
            else logger.LogError("El arranque falló ({ExceptionType}).", exception.GetType().FullName);
        }
        show(message);
        return 1;
    }

    /// <summary>En Linux, un DllNotFoundException (también como excepción interna) al crear la ventana es la falta de WebKitGTK; en otro caso, null.</summary>
    public static string? MissingNativeLibraryMessage(Exception exception, bool isLinux) => isLinux && ContainsDllNotFound(exception)
        ? "Falta WebKitGTK (libwebkit2gtk-4.1). Instálalo con el gestor de paquetes de tu distribución (por ejemplo, sudo apt install libwebkit2gtk-4.1-0 libnotify4) y vuelve a abrir DM Companion."
        : null;

    private static bool ContainsDllNotFound(Exception? exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
            if (e is DllNotFoundException) return true;
        return false;
    }

    public static string UnexpectedFailureMessage(bool hasStarted, string logsFolder) => hasStarted
        ? $"DM Companion se ha detenido por un error. Ábrelo de nuevo; si se repite, revisa los registros en {logsFolder}."
        : $"DM Companion no ha podido arrancar. Cierra otras copias abiertas e inténtalo de nuevo; si se repite, revisa los registros en {logsFolder}.";
}
