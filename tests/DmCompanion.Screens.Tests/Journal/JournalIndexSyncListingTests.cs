using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalIndexSyncListingTests : JournalUiTestBase
{
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";

    public JournalIndexSyncListingTests() =>
        Services.AddSingleton<IVaultServiceFactory>(new CountingFactory { ListSynchronously = true });

    protected override Task SeedAsync()
    {
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"]));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_listing_that_finishes_at_once_still_marks_the_missing_images()
    {
        _ = Journal.Entries;

        await WaitUntilAsync(() => !Journal.Get(Faro)!.Images[0].Exists);

        Journal.Get(Faro)!.Images[0].Exists.Should().BeFalse();
    }
}
