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

public sealed class ShowOnTvTests : BunitContext, IAsyncLifetime
{
    private const string OldLighthouse = "Lugares/Faro Viejo.md";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FakeTvServer _server = new();

    public ShowOnTvTests()
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
        Services.AddScoped<WikilinkInterop>();
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
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    private VaultHost Host => Services.GetRequiredService<VaultHost>();
    private TvSession Tv => Services.GetRequiredService<TvSession>();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Host.OpenAsync(_vault.Root);
        await Host.Current!.SaveAsync("Lugares/Guarida.md", "---\ntype: location\nsecret: true\n---\nGuarida.\n", WritePrecondition.Overwrite);
        await Host.Current.SaveAsync("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n", WritePrecondition.Overwrite);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Host.DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private static IElement? ShowButton<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Mostrar en TV");

    private bool ReaderOffersShow(string path) =>
        ShowButton(Render<NoteReader>(p => p.Add(c => c.Entity, Host.Current!.Index.Get(path)))) is not null;

    private bool SheetOffersShow(string path) =>
        ShowButton(Render<CharacterSheet>(p => p.Add(c => c.Entity, Host.Current!.Index.Get(path)!))) is not null;

    [Fact]
    public async Task Button_only_for_pc_and_location_when_tv_enabled()
    {
        var whileOff = SheetOffersShow("Personajes/Lyra.md");
        await Tv.EnableAsync();

        (whileOff, SheetOffersShow("Personajes/Lyra.md"), ReaderOffersShow(OldLighthouse), ReaderOffersShow("Personajes/Lyra.md"),
                ReaderOffersShow("Personajes/Varek.md"), ReaderOffersShow("Mapas/Boceto de la cueva.md"), ReaderOffersShow("Lugares/Guarida.md"),
                ReaderOffersShow("Monstruos/Lobo.md"))
            .Should().Be((false, true, true, true, false, false, false, false));
    }

    [Fact]
    public async Task Button_sends_the_note_to_the_tv()
    {
        await Tv.EnableAsync();
        var cut = Render<NoteReader>(p => p.Add(c => c.Entity, Host.Current!.Index.Get(OldLighthouse)));

        await cut.InvokeAsync(() => ShowButton(cut)!.Click());

        cut.WaitForAssertion(() => (Tv.Mode, Tv.ContentPath).Should().Be((TvMode.Content, OldLighthouse)), Wait);
    }

    [Fact]
    public async Task Tv_sheet_stats_are_the_ones_the_tablet_sheet_shows()
    {
        await Tv.EnableAsync();
        var sheet = Render<CharacterSheet>(p => p.Add(c => c.Entity, Host.Current!.Index.Get("Personajes/Lyra.md")!));
        var tablet = sheet.FindAll(".ds-stat")
            .Select(s => (s.QuerySelector(".ds-stat__label")!.TextContent.Trim(), s.QuerySelector(".ds-stat__value")!.TextContent.Trim()))
            .ToList();

        await Tv.ShowContentAsync("Personajes/Lyra.md");

        var stats = System.Text.Json.JsonDocument.Parse(_server.Published[^1]).RootElement.GetProperty("sheet").GetProperty("stats");
        var tv = stats.EnumerateArray().Select(s => (s.GetProperty("label").GetString()!, s.GetProperty("value").GetString()!)).ToList();
        (tablet.Count > 5, tv.SequenceEqual(tablet)).Should().Be((true, true));
    }

    [Fact]
    public async Task Now_showing_bar_shows_title_and_back_to_map()
    {
        await Tv.EnableAsync();
        var cut = Render<DmShell>();

        await Tv.ShowContentAsync(OldLighthouse);
        cut.WaitForAssertion(() => cut.Find(".tv-now-showing").TextContent.Should().Contain("En la TV: Faro Viejo"), Wait);
        await cut.InvokeAsync(() => cut.Find(".tv-now-showing button").Click());

        cut.WaitForAssertion(() => (Tv.Mode, cut.FindAll(".tv-now-showing").Count).Should().Be((TvMode.Waiting, 0)), Wait);
    }

    [Fact]
    public async Task Tv_panel_offers_back_to_map_while_showing_content()
    {
        await Tv.EnableAsync();
        await Tv.ShowContentAsync(OldLighthouse);
        var cut = Render<DmShell>();
        cut.Find("button[aria-label='TV']").Click();

        cut.WaitForAssertion(() => cut.Find(".tv-panel .tv-panel__showing").TextContent.Trim().Should().Be("En la TV: Faro Viejo"), Wait);
        await cut.InvokeAsync(() => cut.FindAll(".tv-panel button").First(b => b.TextContent.Trim() == "Volver al mapa").Click());

        cut.WaitForAssertion(() => Tv.Mode.Should().Be(TvMode.Waiting), Wait);
    }
}
