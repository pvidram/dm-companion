using DmCompanion.Screens.Platform;

namespace DmCompanion.Desktop.Tests;

internal sealed class InMemoryAppPreferences : IAppPreferences
{
    private readonly Dictionary<string, string> _values = [];

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string value) => _values[key] = value;

    public void Remove(string key) => _values.Remove(key);
}

internal sealed class FakeBrowserLauncher : IBrowserLauncher
{
    public List<string> Opened { get; } = [];

    public void Open(string url) => Opened.Add(url);
}

internal sealed class FakeLogFolder : ILogFolder
{
    public bool IsAvailable => true;

    public bool Open() => true;
}
