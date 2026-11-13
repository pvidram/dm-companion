using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Desktop.Platform;

public sealed class DesktopSampleVault(DesktopPaths paths) : ISampleVault
{
    public bool IsAvailable => Directory.Exists(paths.SampleSource);

    public async Task<string> PrepareAsync()
    {
        await DirectoryCopy.CopyIfMissingAsync(paths.SampleSource, paths.SampleTarget);
        return paths.SampleTarget;
    }
}
