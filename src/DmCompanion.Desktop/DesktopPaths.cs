namespace DmCompanion.Desktop;

/// <summary>Rutas de datos del host de escritorio. Las claves Desktop:* sustituyen a las carpetas reales (tests).</summary>
public sealed class DesktopPaths
{
    private const string AppFolder = "DmCompanion";

    public DesktopPaths(IConfiguration config)
    {
        if (config["Desktop:DataRoot"] is { Length: > 0 } dataRoot)
        {
            RoamingRoot = Path.Combine(dataRoot, "Roaming", AppFolder);
            LocalRoot = Path.Combine(dataRoot, "Local", AppFolder);
        }
        else
        {
            RoamingRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder);
            LocalRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder);
        }

        var documents = config["Desktop:DocumentsRoot"] is { Length: > 0 } documentsRoot
            ? documentsRoot
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        DocumentsRoot = documents;
        SampleTarget = Path.Combine(documents, "DM Companion", "Ejemplo");
        SampleSource = config["Desktop:SampleSource"] is { Length: > 0 } source
            ? source
            : Path.Combine(AppContext.BaseDirectory, "ejemplo");

        Directory.CreateDirectory(RoamingRoot);
        Directory.CreateDirectory(LocalRoot);
    }

    public string RoamingRoot { get; }

    public string LocalRoot { get; }

    public string DocumentsRoot { get; }

    public string PreferencesFile => Path.Combine(RoamingRoot, "prefs.json");

    public string WindowFile => Path.Combine(RoamingRoot, "window.json");

    public string LogsFolder => Path.Combine(LocalRoot, "logs");

    public string InstanceFile => Path.Combine(LocalRoot, "instance.json");

    public string SampleTarget { get; }

    public string SampleSource { get; }
}
