using System.Collections.Concurrent;
using System.Globalization;

namespace DmCompanion.Desktop.Logging;

/// <summary>Escribe los registros en <c>dm-companion-AAAAMMDD.log</c> (fecha local) y conserva los <paramref name="keepDays"/> ficheros más recientes.</summary>
public sealed class DailyFileLoggerProvider(string folder, TimeProvider time, int keepDays = 7) : ILoggerProvider
{
    private const string Prefix = "dm-companion-";
    private const string Extension = ".log";

    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<string, DailyFileLogger> _loggers = new();
    private string? _currentFile;
    private bool _failureReported;

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new DailyFileLogger(name, this));

    public void Dispose() => _loggers.Clear();

    internal void Write(string line)
    {
        lock (_gate)
        {
            try
            {
                var now = time.GetLocalNow();
                var file = Path.Combine(folder, Prefix + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + Extension);
                if (file != _currentFile)
                {
                    Directory.CreateDirectory(folder);
                    // Se asigna antes de limpiar: si la limpieza falla no se reintenta en cada línea.
                    _currentFile = file;
                    DeleteOldFiles(file);
                }
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Reintenta en la siguiente línea: la carpeta puede volver a estar disponible.
                _currentFile = null;
                if (_failureReported) return;
                _failureReported = true;
                Console.Error.WriteLine("No se pueden escribir los registros");
            }
        }
    }

    internal DateTimeOffset Now => time.GetLocalNow();

    private void DeleteOldFiles(string currentFile)
    {
        // Los nombres llevan la fecha en AAAAMMDD, así que el orden alfabético es el cronológico.
        var old = Directory.GetFiles(folder, Prefix + "*" + Extension)
            .Where(f => f != currentFile)
            .OrderDescending(StringComparer.Ordinal)
            .Skip(Math.Max(keepDays - 1, 0));
        foreach (var file in old)
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* en uso: se borrará otro día */ }
        }
    }
}
