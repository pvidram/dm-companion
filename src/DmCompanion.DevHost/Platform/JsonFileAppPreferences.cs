using System.Text.Json;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.DevHost.Platform;

public sealed class JsonFileAppPreferences(string filePath, ILogger<JsonFileAppPreferences>? logger = null) : IAppPreferences
{
    private readonly Lock _gate = new();
    private Dictionary<string, string>? _values;

    public string? Get(string key)
    {
        lock (_gate) return Load().GetValueOrDefault(key);
    }

    public void Set(string key, string value)
    {
        lock (_gate)
        {
            Load()[key] = value;
            Save();
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            if (Load().Remove(key)) Save();
        }
    }

    // Un prefs.json ilegible no tumba el arranque: se trata como vacío y el siguiente Set lo reescribe.
    private Dictionary<string, string> Load()
    {
        if (_values is not null) return _values;
        _values = [];
        if (!File.Exists(filePath)) return _values;
        try
        {
            _values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath)) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "No se puede leer {File}; se trata como vacío.", filePath);
        }
        return _values;
    }

    // Escritura atómica: fichero temporal en la misma carpeta y renombrado con sobrescritura.
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_values));
        File.Move(temp, filePath, overwrite: true);
    }
}
