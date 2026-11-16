using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class VaultPanelTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly TempVault _other = TempVault.FromDemo();
    private readonly TempVault _plain = TempVault.Empty();

    public VaultPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
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

    private VaultHost Host => Services.GetRequiredService<VaultHost>();
    private ShellState Shell => Services.GetRequiredService<ShellState>();

    Task IAsyncLifetime.InitializeAsync() => Host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Host.DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
        _other.Dispose();
        _plain.Dispose();
    }

    private IRenderedComponent<DmShell> RenderWithPanelOpen()
    {
        var cut = Render<DmShell>();
        cut.Find("button[aria-label='Cambiar vault']").Click();
        cut.WaitForAssertion(() => Panel(cut).HasAttribute("hidden").Should().BeFalse(), Wait);
        return cut;
    }

    private static IElement Panel(IRenderedComponent<DmShell> cut) =>
        cut.FindAll("section.ds-parchment").First(s => s.QuerySelector("h2")!.TextContent == "Cambiar vault");

    private static void Choose(IRenderedComponent<DmShell> cut, string folderPath)
    {
        var name = Path.GetFileName(folderPath);
        cut.WaitForAssertion(() => Panel(cut).QuerySelectorAll("button.ds-list-row").Any(b => b.TextContent.Trim() == name).Should().BeTrue(), Wait);
        cut.InvokeAsync(() => Panel(cut).QuerySelectorAll("button.ds-list-row").First(b => b.TextContent.Trim() == name).Click());
        cut.WaitForAssertion(() => Panel(cut).QuerySelectorAll("p").Any(p => p.TextContent == folderPath).Should().BeTrue(), Wait);
        cut.InvokeAsync(() => Panel(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Usar esta carpeta").Click());
    }

    [Fact]
    public void Browsing_starts_in_the_folder_that_holds_the_open_vault()
    {
        var cut = RenderWithPanelOpen();

        Panel(cut).QuerySelectorAll("p").Select(p => p.TextContent).Should().Contain(Path.GetDirectoryName(_vault.Root));
    }

    [Fact]
    public void Choosing_the_open_vault_only_closes_the_panel()
    {
        var cut = RenderWithPanelOpen();

        Choose(cut, _vault.Root);

        cut.WaitForAssertion(() => (Shell.ActivePanel, Host.Current?.RootPath).Should().Be((OverlayPanel.None, _vault.Root)), Wait);
    }

    [Fact]
    public void Choosing_another_vault_switches_to_it()
    {
        var cut = RenderWithPanelOpen();

        Choose(cut, _other.Root);

        cut.WaitForAssertion(() => (Shell.ActivePanel, Host.Current?.RootPath).Should().Be((OverlayPanel.None, _other.Root)), Wait);
    }

    [Fact]
    public void A_folder_that_does_not_look_like_a_vault_asks_before_switching()
    {
        var cut = RenderWithPanelOpen();

        Choose(cut, _plain.Root);

        cut.WaitForAssertion(() => (Shell.ConfirmingVaultPath, Host.Current?.RootPath).Should().Be((_plain.Root, _vault.Root)), Wait);
    }

    [Fact]
    public void Confirming_a_folder_that_does_not_look_like_a_vault_switches_to_it()
    {
        var cut = RenderWithPanelOpen();
        Choose(cut, _plain.Root);
        cut.WaitForAssertion(() => Shell.ConfirmingVaultPath.Should().Be(_plain.Root), Wait);

        cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Abrir de todos modos").Click());

        cut.WaitForAssertion(() => (Shell.ConfirmingVaultPath, Host.Current?.RootPath).Should().Be((null, _plain.Root)), Wait);
    }

    [Fact]
    public async Task A_vault_that_cannot_open_keeps_the_current_one_and_says_why()
    {
        var cut = RenderWithPanelOpen();
        var missing = Path.Combine(_plain.Root, "no-existe");

        await cut.InvokeAsync(() => VaultBrowsing.SwitchAsync(Host, Shell, missing));

        cut.WaitForAssertion(() => (Host.Current?.RootPath, Shell.ActivePanel, Panel(cut).QuerySelector("[role=alert]") is not null)
            .Should().Be((_vault.Root, OverlayPanel.Vault, true)), Wait);
    }
}
