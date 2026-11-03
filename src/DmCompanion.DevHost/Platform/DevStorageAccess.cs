using DmCompanion.DevHost.Services;
using DmCompanion.Screens.Platform;

namespace DmCompanion.DevHost.Platform;

public sealed class DevStorageAccess(DevVaultSeeder seeder) : IStorageAccess
{
    public bool HasAllFilesAccess => true;

    public string DefaultBrowseRoot => Path.GetDirectoryName(seeder.VaultPath) ?? seeder.VaultPath;

    public void RequestAllFilesAccess() { }
}
