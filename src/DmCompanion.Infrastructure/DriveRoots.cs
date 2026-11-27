namespace DmCompanion.Infrastructure;

public sealed record DriveEntry(string RootPath, DriveType Type, bool IsReady);

public static class DriveRoots
{
    // On Unix, DriveInfo lists every mount point (tracefs, overlay, /, …) and reports WSL's /mnt/c as Network,
    // so the drive type says nothing useful there: only the usual places for user mounts are offered.
    private static readonly string[] UnixUserMountParents = ["/mnt/", "/media/", "/run/media/"];

    public static IReadOnlyList<string> From(IEnumerable<DriveEntry> drives, bool unix) =>
        drives
            .Where(d => d.IsReady && (unix ? IsUnixUserMount(d) : d.Type is DriveType.Fixed or DriveType.Removable))
            .Select(d => d.RootPath)
            .Distinct(unix ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> Current() =>
        From(DriveInfo.GetDrives().Select(d => new DriveEntry(d.Name, d.DriveType, SafeIsReady(d))), unix: !OperatingSystem.IsWindows());

    private static bool IsUnixUserMount(DriveEntry drive) =>
        drive.Type is not DriveType.Ram
        && UnixUserMountParents.Any(parent => drive.RootPath.StartsWith(parent, StringComparison.Ordinal))
        && !IsWslInternal(drive.RootPath);

    private static bool IsWslInternal(string path) =>
        path is "/mnt/wsl" or "/mnt/wslg" || path.StartsWith("/mnt/wsl/", StringComparison.Ordinal) || path.StartsWith("/mnt/wslg/", StringComparison.Ordinal);

    // IsReady puede lanzar en unidades que desaparecen mientras se enumeran.
    private static bool SafeIsReady(DriveInfo drive)
    {
        try { return drive.IsReady; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
