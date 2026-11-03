using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Tests;

internal sealed class InMemoryAppPreferences : IAppPreferences
{
    private readonly Dictionary<string, string> _values = [];

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string value) => _values[key] = value;

    public void Remove(string key) => _values.Remove(key);
}
