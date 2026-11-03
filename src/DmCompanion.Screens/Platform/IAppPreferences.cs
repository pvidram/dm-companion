namespace DmCompanion.Screens.Platform;

public interface IAppPreferences
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
}
