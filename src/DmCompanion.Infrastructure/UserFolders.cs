namespace DmCompanion.Infrastructure;

/// <summary>Home, Desktop, Documents and Downloads, when they exist, as quick-access shortcuts of the folder picker.</summary>
public static class UserFolders
{
    public static IReadOnlyList<string> From(IEnumerable<string?> candidates, Func<string, bool> exists) =>
        candidates
            .OfType<string>()
            .Where(path => path.Length > 0 && exists(path))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<string> Current()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return From(
            [
                home,
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                home.Length > 0 ? Path.Combine(home, "Downloads") : null,
            ],
            Directory.Exists);
    }
}
