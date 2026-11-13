using System.Text.Json;

namespace DmCompanion.Desktop;

/// <summary>Garantiza una sola instancia por sesión de usuario con un mutex con nombre.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Devuelve null si ya hay otra instancia viva. Con <paramref name="instanceFile"/>, borra el de una ejecución anterior.</summary>
    public static SingleInstance? TryAcquire(string name = @"Local\DmCompanion.Desktop", string? instanceFile = null)
    {
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        if (createdNew)
        {
            if (instanceFile is not null) DeleteUrl(instanceFile);
            return new SingleInstance(mutex);
        }
        mutex.Dispose();
        return null;
    }

    public static void WriteUrl(string instanceFile, string url)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(instanceFile)!);
        File.WriteAllText(instanceFile, JsonSerializer.Serialize(new Dictionary<string, string> { ["url"] = url }));
    }

    public static void DeleteUrl(string instanceFile)
    {
        try
        {
            File.Delete(instanceFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Un instance.json que no se puede borrar solo causa una URL obsoleta; no debe impedir arrancar.
        }
    }

    /// <summary>Borra <paramref name="instanceFile"/> solo si <paramref name="owner"/> posee el mutex (no es null).</summary>
    public static void DeleteUrlIfOwner(SingleInstance? owner, string instanceFile)
    {
        if (owner is not null) DeleteUrl(instanceFile);
    }

    public static string? ReadUrl(string instanceFile)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(instanceFile));
            return values is not null && values.TryGetValue("url", out var url) && url.Length > 0 ? url : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Sondea <paramref name="instanceFile"/> hasta que haya URL o pase <paramref name="timeout"/>; null si no aparece.</summary>
    public static async Task<string?> WaitForUrlAsync(string instanceFile, TimeProvider time, TimeSpan timeout, TimeSpan? interval = null)
    {
        var step = interval ?? TimeSpan.FromMilliseconds(250);
        var deadline = time.GetUtcNow() + timeout;
        while (true)
        {
            if (ReadUrl(instanceFile) is { } url) return url;
            if (time.GetUtcNow() >= deadline) return null;
            await Task.Delay(step, time);
        }
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // El mutex ya no pertenece a este hilo (p. ej. Dispose desde otro hilo): el sistema lo libera al cerrar el proceso.
        }
        _mutex.Dispose();
    }
}
