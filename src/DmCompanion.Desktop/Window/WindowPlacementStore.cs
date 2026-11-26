using System.Text.Json;

namespace DmCompanion.Desktop.Window;

/// <summary>Guarda la posición de la ventana en window.json. Un fichero ausente o dañado equivale a «sin posición».</summary>
public sealed class WindowPlacementStore(string file, ILogger<WindowPlacementStore> logger)
{
    public WindowBounds? Load()
    {
        if (!File.Exists(file)) return null;
        try
        {
            var bounds = JsonSerializer.Deserialize<WindowBounds>(File.ReadAllText(file));
            return bounds is { Width: > 0, Height: > 0 } ? bounds : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo leer la posición de la ventana ({ExceptionType}).", ex.GetType().Name);
            return null;
        }
    }

    public void Save(WindowBounds bounds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(bounds));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo guardar la posición de la ventana ({ExceptionType}).", ex.GetType().Name);
        }
    }
}
