using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalLogSectionFaultTests : JournalUiTestBase
{
    private readonly JournalFaultyFactory _factory = new();

    public JournalLogSectionFaultTests()
    {
        Services.AddSingleton<IVaultServiceFactory>(_factory);
        Services.AddSingleton<JournalLogService>();
    }

    protected override Task SeedAsync()
    {
        WriteFile(LyraDiary + "/2026-11-16 Ankheg.md", EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"]));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        return Task.CompletedTask;
    }

    [Fact]
    public void An_unexpected_failure_while_generating_shows_an_error_instead_of_escaping_the_handler()
    {
        var cut = Render<JournalLogSection>();
        _factory.Storage!.ThrowOnNoteWrites = true;

        cut.FindAll("button").First(b => b.TextContent.Contains("Generar bitácora común", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Trim().Should().Be("No se pudo generar la bitácora."), Wait);
    }
}
