namespace DmCompanion.DevHost;

internal static class DevHostPaths
{
    public static string DataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dm-companion-devhost");

    public static string DefaultVault => Path.Combine(DataRoot, "vault-demo");

    public static string PreferencesFile => Path.Combine(DataRoot, "prefs.json");
}
