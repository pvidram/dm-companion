namespace DmCompanion.Infrastructure;

public sealed record DriveEntry(string RootPath, DriveType Type, bool IsReady);

public static class DriveRoots
{
    public static IReadOnlyList<string> From(IEnumerable<DriveEntry> drives) =>
        drives
            .Where(d => d.IsReady && d.Type is DriveType.Fixed or DriveType.Removable)
            .Select(d => d.RootPath)
            .ToList();

    public static IReadOnlyList<string> Current() =>
        From(DriveInfo.GetDrives().Select(d => new DriveEntry(d.Name, d.DriveType, SafeIsReady(d))));

    // IsReady puede lanzar en unidades que desaparecen mientras se enumeran.
    private static bool SafeIsReady(DriveInfo drive)
    {
        try { return drive.IsReady; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
