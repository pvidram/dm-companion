using DmCompanion.Screens.Platform;

namespace DmCompanion.Desktop.Tests;

// Sustituye a GitHubUpdateCheck en los tests que arrancan el Main real: nunca sale a la red.
public sealed class NoNetworkUpdateCheck : IUpdateCheck
{
    public Task<AvailableUpdate?> GetLatestAsync(CancellationToken ct) => Task.FromResult<AvailableUpdate?>(null);

    public void OpenReleasePage(AvailableUpdate update) { }
}
