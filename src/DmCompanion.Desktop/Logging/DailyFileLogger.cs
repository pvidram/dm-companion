using System.Globalization;

namespace DmCompanion.Desktop.Logging;

internal sealed class DailyFileLogger(string category, DailyFileLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and not LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        // De la excepción solo va el tipo: el mensaje y la traza pueden citar rutas del vault o texto de las notas.
        // Las plantillas del framework (Microsoft.*, System.*) pasan el texto de la excepción como parámetro, así que con
        // excepción no se escribe su mensaje formateado, solo el EventId. Los mensajes de la app los controla el código propio.
        var frameworkFailure = exception is not null && IsFrameworkCategory(category);
        var message = frameworkFailure ? eventId.Name is null ? $"[{eventId.Id}]" : $"[{eventId.Id} {eventId.Name}]" : formatter(state, exception);
        var line = $"{provider.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)} {Label(logLevel)} {category}: {message}";
        if (exception is not null) line += $" [{exception.GetType().Name}]";
        provider.Write(line);
    }

    private static bool IsFrameworkCategory(string category) =>
        category.StartsWith("Microsoft.", StringComparison.Ordinal) || category.StartsWith("System.", StringComparison.Ordinal);

    private static string Label(LogLevel level) => level switch
    {
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => level.ToString().ToUpperInvariant().PadRight(5),
    };
}
