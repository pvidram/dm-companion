using DmCompanion.Core.Glossary;

namespace DmCompanion.Core.Tests.Glossary;

/// <summary>Reads packs from a folder (by default the fixtures copied to the output directory).</summary>
public sealed class FixturePackSource(string folder) : IGlossaryPackSource
{
    public static string DefaultFolder => Path.Combine(AppContext.BaseDirectory, "Glossary", "Fixtures");

    public FixturePackSource() : this(DefaultFolder) { }

    public Stream? Open(string packName)
    {
        var path = Path.Combine(folder, packName + ".json");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    /// <summary>Copies the fixtures to a new temp folder so a test can delete or corrupt packs.</summary>
    public static string CopyToTemp()
    {
        var temp = Path.Combine(Path.GetTempPath(), "glossary-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        foreach (var file in Directory.GetFiles(DefaultFolder, "*.json"))
            File.Copy(file, Path.Combine(temp, Path.GetFileName(file)));
        return temp;
    }
}
