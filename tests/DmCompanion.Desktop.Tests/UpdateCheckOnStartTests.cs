using DmCompanion.Desktop.Updates;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;

namespace DmCompanion.Desktop.Tests;

public sealed class UpdateCheckOnStartTests
{
    private sealed class ThrowingCheck : IUpdateCheck
    {
        public Task<AvailableUpdate?> GetLatestAsync(CancellationToken ct) => throw new FormatException("token");

        public void OpenReleasePage(AvailableUpdate update) { }
    }

    [Fact]
    public async Task A_throwing_check_does_not_fault_the_service_and_is_logged()
    {
        var logger = new ListLogger<UpdateCheckOnStart>();
        using var service = new UpdateCheckOnStart(new UpdateNotice(new InMemoryAppPreferences(), new ThrowingCheck()), logger);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        (service.ExecuteTask!.IsCompletedSuccessfully, logger.Entries.Single().Message).Should()
            .Be((true, "Falló la comprobación de versiones al arrancar (FormatException)."));
    }
}
