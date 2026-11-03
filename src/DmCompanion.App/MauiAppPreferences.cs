using DmCompanion.Screens.Platform;

namespace DmCompanion.App;

public sealed class MauiAppPreferences : IAppPreferences
{
    public string? Get(string key) =>
        Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string?>(key, null) : null;

    public void Set(string key, string value) => Preferences.Default.Set(key, value);

    public void Remove(string key) => Preferences.Default.Remove(key);
}
