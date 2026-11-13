using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Desktop.Platform;

public sealed class DesktopStorageAccess(DesktopPaths paths) : IStorageAccess
{
    public bool HasAllFilesAccess => true;

    public string DefaultBrowseRoot => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public void RequestAllFilesAccess() { }

    public bool CanTypePath => true;

    public IReadOnlyList<string> Roots => DriveRoots.Current();

    public string? InitialPath => paths.DocumentsRoot;
}
