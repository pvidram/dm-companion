using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class DmShellJournalTests : JournalUiTestBase
{
    public DmShellJournalTests()
    {
        Services.AddSoundForTests();
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddWindowingForTests();
        Services.AddVaultBrowsingForTests();
        Services.AddSingleton<JournalStore>();
    }

    [Fact]
    public void The_shell_has_the_journal_form_and_the_delete_confirmation()
    {
        var cut = Render<DmShell>();

        cut.FindAll("h2.ds-dialog__title").Select(h => h.TextContent).Should().Contain(["Nueva entrada", "Borrar entrada"]);
    }

    [Fact]
    public async Task Opening_the_form_from_the_shell_state_shows_it()
    {
        var cut = Render<DmShell>();

        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Lyra)));

        cut.WaitForAssertion(() => cut.FindAll(".ds-dialog-backdrop").Count(d => !d.HasAttribute("hidden")).Should().Be(1), Wait);
    }
}
