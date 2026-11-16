using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class DmShellTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly InMemoryAppPreferences _preferences = new();

    public DmShellTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(_preferences);
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddGlossaryForTests();
        Services.AddSoundForTests();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
        Services.AddVaultBrowsingForTests();
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private static string Pressed(IRenderedComponent<DmShell> cut, string label) =>
        cut.Find($"button[aria-label='{label}']").GetAttribute("aria-pressed")!;

    private static bool IsOpen(IRenderedComponent<DmShell> cut, string title) =>
        !cut.FindAll("section.ds-parchment").First(s => s.QuerySelector("h2")!.TextContent == title).HasAttribute("hidden");

    [Fact]
    public void Rail_has_the_three_buttons_unpressed_at_start()
    {
        var cut = Render<DmShell>();

        new[] { Pressed(cut, "Jugadores"), Pressed(cut, "Notas") }.Should().AllBe("false");
        cut.Find("button[aria-label='Cambiar vault']").Should().NotBeNull();
    }

    [Fact]
    public void Change_vault_button_sits_apart_at_the_bottom_of_the_rail()
    {
        var cut = Render<DmShell>();

        var rail = cut.Find("nav.ds-frame__rail");
        var bottom = rail.Children.Last();
        (bottom.ClassList.Contains("dm-shell__rail-bottom"), bottom.QuerySelector("button")?.GetAttribute("aria-label"))
            .Should().Be((true, "Cambiar vault"));
    }

    [Fact]
    public void Pressed_state_follows_the_active_panel()
    {
        var cut = Render<DmShell>();

        cut.Find("button[aria-label='Notas']").Click();

        cut.WaitForAssertion(() => Pressed(cut, "Notas").Should().Be("true"), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Opening_notes_then_players_leaves_only_players_open()
    {
        var cut = Render<DmShell>();

        cut.Find("button[aria-label='Notas']").Click();
        cut.WaitForAssertion(() => IsOpen(cut, "Notas").Should().BeTrue(), TimeSpan.FromSeconds(10));
        cut.Find("button[aria-label='Jugadores']").Click();

        cut.WaitForAssertion(() => (IsOpen(cut, "Notas"), IsOpen(cut, "Jugadores")).Should().Be((false, true)), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Both_parchments_stay_in_the_dom_when_closed()
    {
        var cut = Render<DmShell>();

        cut.FindAll("section.ds-parchment").Should().HaveCount(7);
    }

    [Fact]
    public void Closing_a_parchment_keeps_it_mounted_and_hidden()
    {
        var cut = Render<DmShell>();
        cut.Find("button[aria-label='Notas']").Click();
        cut.WaitForAssertion(() => IsOpen(cut, "Notas").Should().BeTrue(), TimeSpan.FromSeconds(10));

        cut.Find("button[aria-label='Notas']").Click();

        cut.WaitForAssertion(() => (cut.FindAll("section.ds-parchment").Count, IsOpen(cut, "Notas")).Should().Be((7, false)), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Change_vault_opens_its_panel_and_keeps_the_vault_open()
    {
        var cut = Render<DmShell>();

        cut.Find("button[aria-label='Cambiar vault']").Click();

        cut.WaitForAssertion(() => (IsOpen(cut, "Cambiar vault"), Services.GetRequiredService<VaultHost>().Current is not null)
            .Should().Be((true, true)), TimeSpan.FromSeconds(10));
    }
}
