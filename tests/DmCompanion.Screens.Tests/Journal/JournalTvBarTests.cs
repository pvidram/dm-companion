using Bunit;
using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class JournalTvBarTests : BunitContext, IAsyncLifetime
{
    private const string Diary = "Personajes/Lyra/Diario";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly VaultHost _host;
    private readonly TvSession _tv;

    public JournalTvBarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        var prefs = new InMemoryAppPreferences();
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), prefs, NullLogger<VaultHost>.Instance);
        var maps = new MapSession(_host, prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        var images = new NoteImageResolver(_host);
        var feed = new TvMapFeed(_host, new TokenHitPoints(_host, GlossaryTestServices.CreateGlossary(_host)), images);
        _tv = new TvSession(new FakeTvServer(), _host, maps, feed, new TvContentFeed(_host, images, feed), prefs, NullLogger<TvSession>.Instance);
        Services.AddSingleton(_tv);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _host.OpenAsync(_vault.Root);
        await _tv.EnableAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _host.DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private static JournalEntry EntryOf(string title, string added, string[] images, string note = "") =>
        JournalReader.Read($"{Diary}/{title}.md",
            $"---\ntype: journal\nadded: {added}\nimages:\n{string.Concat(images.Select(name => $"  - \"{name}\"\n"))}---\n{note}\n",
            DateTime.UtcNow, _ => true);

    private Task ShowLogAsync()
    {
        Directory.CreateDirectory(_vault.PathOf(Diary));
        foreach (var name in new[] { "a.png", "b.png" }) File.WriteAllBytes(_vault.PathOf($"{Diary}/{name}"), [1, 2, 3]);
        return _tv.ShowJournalLogAsync([EntryOf("2026-11-10 Vieja", "2026-11-10T20:00:00+01:00", ["a.png"]), EntryOf("2026-11-16 Nueva", "2026-11-16T21:34:00+01:00", ["b.png"])]);
    }

    private static string Position(IRenderedComponent<JournalTvBar> cut) => cut.Find(".journal-tv-bar__position").TextContent;

    private static AngleSharp.Dom.IElement ButtonOf(IRenderedComponent<JournalTvBar> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == label);

    [Fact]
    public async Task The_bar_of_the_log_says_which_entry_is_on_screen()
    {
        await ShowLogAsync();

        Position(Render<JournalTvBar>(p => p.Add(c => c.Tv, _tv))).Should().Be("Entrada 1 de 2");
    }

    [Fact]
    public async Task At_the_start_Anterior_is_disabled()
    {
        await ShowLogAsync();

        var cut = Render<JournalTvBar>(p => p.Add(c => c.Tv, _tv));

        (ButtonOf(cut, "Anterior").HasAttribute("disabled"), ButtonOf(cut, "Siguiente").HasAttribute("disabled")).Should().Be((true, false));
    }

    [Fact]
    public async Task Siguiente_moves_the_tv_and_the_label_follows()
    {
        await ShowLogAsync();
        var cut = Render<JournalTvBar>(p => p.Add(c => c.Tv, _tv));

        ButtonOf(cut, "Siguiente").Click();

        cut.WaitForAssertion(() => (Position(cut), _tv.Journal!.Position).Should().Be(("Entrada 2 de 2", 1)), Wait);
    }

    [Fact]
    public async Task At_the_end_Siguiente_is_disabled_and_Anterior_goes_back()
    {
        await ShowLogAsync();
        var cut = Render<JournalTvBar>(p => p.Add(c => c.Tv, _tv));
        ButtonOf(cut, "Siguiente").Click();
        cut.WaitForAssertion(() => ButtonOf(cut, "Siguiente").HasAttribute("disabled").Should().BeTrue(), Wait);

        ButtonOf(cut, "Anterior").Click();

        cut.WaitForAssertion(() => Position(cut).Should().Be("Entrada 1 de 2"), Wait);
    }

    [Fact]
    public async Task The_bar_of_an_entry_says_image_and_note()
    {
        Directory.CreateDirectory(_vault.PathOf(Diary));
        File.WriteAllBytes(_vault.PathOf($"{Diary}/a.png"), [1, 2, 3]);
        await _tv.ShowJournalEntryAsync(EntryOf("2026-11-16 Ankheg", "2026-11-16T21:34:00+01:00", ["a.png"], "Nota"));
        var cut = Render<JournalTvBar>(p => p.Add(c => c.Tv, _tv));
        var first = Position(cut);

        ButtonOf(cut, "Siguiente").Click();

        cut.WaitForAssertion(() => (first, Position(cut)).Should().Be(("Imagen 1 de 1", "Nota")), Wait);
    }

    [Fact]
    public void Outside_the_diary_the_bar_renders_nothing() =>
        Render<JournalTvBar>(p => p.Add(c => c.Tv, _tv)).Markup.Trim().Should().BeEmpty();

    [Fact]
    public async Task The_now_showing_notice_carries_the_bar_and_Volver_al_mapa()
    {
        await ShowLogAsync();

        var cut = Render<TvNowShowing>();

        (cut.Markup.Contains("En la TV: Bitácora", StringComparison.Ordinal), string.Join("|", cut.FindAll("button").Select(b => b.TextContent.Trim())))
            .Should().Be((true, "Anterior|Siguiente|Volver al mapa"));
    }

    [Fact]
    public async Task Volver_al_mapa_in_the_notice_stops_the_slides()
    {
        await ShowLogAsync();
        var cut = Render<TvNowShowing>();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Volver al mapa").Click();

        cut.WaitForAssertion(() => (_tv.Mode, _tv.Journal).Should().Be((TvMode.Waiting, null)), Wait);
    }
}
