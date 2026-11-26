using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Tv;
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

public abstract class TvPanelTestBase : BunitContext, IAsyncLifetime
{
    protected static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private protected readonly FakeTvServer _server = new();

    protected TvPanelTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
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
        Services.AddWindowingForTests();
        Services.AddVaultBrowsingForTests();
        Services.AddSingleton<ITvServer>(_server);
        Services.AddSingleton<TvMapFeed>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    protected const string Cave = "Mapas/Boceto de la cueva.md";

    protected VaultHost Host => Services.GetRequiredService<VaultHost>();
    protected MapSession Maps => Services.GetRequiredService<MapSession>();
    protected TvSession Tv => Services.GetRequiredService<TvSession>();

    Task IAsyncLifetime.InitializeAsync() => Host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Host.DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    protected static IElement Panel(IRenderedComponent<DmShell> cut) =>
        cut.FindAll("section.ds-parchment").First(s => s.QuerySelector("h2")!.TextContent == "TV");

    protected static IElement ButtonIn(IElement panel, string text) =>
        panel.QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);

    protected IRenderedComponent<DmShell> RenderWithPanelOpen()
    {
        var cut = Render<DmShell>();
        cut.Find("button[aria-label='TV']").Click();
        cut.WaitForAssertion(() => Panel(cut).HasAttribute("hidden").Should().BeFalse(), Wait);
        return cut;
    }

    protected IRenderedComponent<DmShell> RenderEnabled()
    {
        var cut = RenderWithPanelOpen();
        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Activar TV").Click());
        cut.WaitForAssertion(() => Tv.Mode.Should().Be(TvMode.Waiting), Wait);
        return cut;
    }
}
