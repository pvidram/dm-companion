using Microsoft.Extensions.Logging;
using AndroidLog = Android.Util.Log;

namespace DmCompanion.App;

// Sink mínimo a logcat que sí existe en Release. Privacidad: el texto que llega aquí lo componen los
// call sites con rutas relativas y tipos/mensajes de excepción; nunca contenido de notas.
public sealed class AndroidLogcatLoggerProvider : ILoggerProvider
{
    public const string Tag = "DmCompanion";

    public ILogger CreateLogger(string categoryName) => new AndroidLogcatLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class AndroidLogcatLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var text = $"{category}: {formatter(state, exception)}";
            if (exception is not null) text += $"{Environment.NewLine}{exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception.StackTrace}";

            switch (logLevel)
            {
                case LogLevel.Trace: AndroidLog.Verbose(Tag, text); break;
                case LogLevel.Debug: AndroidLog.Debug(Tag, text); break;
                case LogLevel.Information: AndroidLog.Info(Tag, text); break;
                case LogLevel.Warning: AndroidLog.Warn(Tag, text); break;
                default: AndroidLog.Error(Tag, text); break;
            }
        }
    }
}
