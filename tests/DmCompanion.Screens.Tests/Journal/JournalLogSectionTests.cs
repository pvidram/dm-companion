using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Journal;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalLogSectionTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Lobo = LyraDiary + "/2026-11-10 Lobo.md";
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";

    public JournalLogSectionTests()
    {
        Services.AddSingleton<JournalLogService>();
        Services.AddSingleton<ITvServer>(new FakeTvServer());
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<TvMapFeed>();
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"]));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteFile(Lobo, EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"]));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"]));
        return Task.CompletedTask;
    }

    private TvSession Tv => Services.GetRequiredService<TvSession>();

    private static string[] Buttons(IRenderedComponent<JournalLogSection> cut) =>
        [.. cut.FindAll("button").Select(b => b.TextContent.Trim())];

    private static IElement ButtonLabeled(IRenderedComponent<JournalLogSection> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(label, StringComparison.Ordinal));

    [Fact]
    public void The_section_has_a_heading_and_only_Generar_when_there_is_no_tv_and_no_note()
    {
        var cut = Render<JournalLogSection>();

        (cut.Find("h3").TextContent, string.Join("|", Buttons(cut))).Should().Be(("Bitácora", "Generar bitácora común"));
    }

    [Fact]
    public async Task Generar_reports_the_number_of_entries()
    {
        var cut = Render<JournalLogSection>();

        await cut.InvokeAsync(() => ButtonLabeled(cut, "Generar bitácora común").Click());

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Trim().Should().Be("Bitácora generada: 3 entradas"), Wait);
    }

    [Fact]
    public async Task After_generating_the_note_can_be_opened()
    {
        var cut = Render<JournalLogSection>();
        await cut.InvokeAsync(() => ButtonLabeled(cut, "Generar bitácora común").Click());

        cut.WaitForAssertion(() => Buttons(cut).Should().Contain("Abrir nota de la bitácora"), Wait);
    }

    [Fact]
    public async Task Abrir_nota_opens_it_in_the_notes_panel()
    {
        WriteFile(JournalPaths.LogPath, "---\ntype: journal-log\ngenerated: true\n---\nVieja.\n");
        await Host.OpenAsync(Vault.Root);
        var cut = Render<JournalLogSection>();

        await cut.InvokeAsync(() => ButtonLabeled(cut, "Abrir nota de la bitácora").Click());

        cut.WaitForAssertion(() => (Shell.ActivePanel, Shell.SelectedNotePath).Should().Be((OverlayPanel.Notes, (string?)JournalPaths.LogPath)), Wait);
    }

    [Fact]
    public async Task A_foreign_note_gets_the_warning_and_stays_untouched()
    {
        const string mine = "---\ntype: note\n---\nMi propia bitácora.\n";
        WriteFile(JournalPaths.LogPath, mine);
        var cut = Render<JournalLogSection>();

        await cut.InvokeAsync(() => ButtonLabeled(cut, "Generar bitácora común").Click());

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Trim().Should().Be(JournalLogService.ForeignNoteText), Wait);
        File.ReadAllText(Vault.PathOf(JournalPaths.LogPath)).Should().Be(mine);
    }

    [Fact]
    public async Task The_notice_can_be_closed()
    {
        var cut = Render<JournalLogSection>();
        await cut.InvokeAsync(() => ButtonLabeled(cut, "Generar bitácora común").Click());
        cut.WaitForAssertion(() => cut.FindAll(".ds-notice").Count.Should().Be(1), Wait);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Cerrar aviso']").Click());

        cut.FindAll(".ds-notice").Count.Should().Be(0);
    }

    [Fact]
    public void Ver_bitacora_en_TV_is_hidden_while_the_tv_is_off() =>
        Buttons(Render<JournalLogSection>()).Should().NotContain("Ver bitácora en TV");

    [Fact]
    public async Task Ver_bitacora_en_TV_appears_when_the_tv_is_on()
    {
        var cut = Render<JournalLogSection>();

        await Tv.EnableAsync();

        cut.WaitForAssertion(() => Buttons(cut).Should().Contain("Ver bitácora en TV"), Wait);
    }

    [Fact]
    public async Task Ver_bitacora_en_TV_plays_the_live_index_in_log_order()
    {
        await Tv.EnableAsync();
        await Journal.RefreshImagesAsync();
        var cut = Render<JournalLogSection>();

        await cut.InvokeAsync(() => ButtonLabeled(cut, "Ver bitácora en TV").Click());

        cut.WaitForAssertion(() => (Tv.Mode, Tv.Journal?.IsLog, Tv.Journal?.Title, Tv.Journal?.Slides.Count, Tv.Journal?.Current.Title)
            .Should().Be((TvMode.Content, (bool?)true, "Bitácora", (int?)2, "Lobo")), Wait);
    }

    [Fact]
    public async Task Ver_bitacora_en_TV_does_not_need_the_generated_note()
    {
        await Tv.EnableAsync();
        var cut = Render<JournalLogSection>();

        await cut.InvokeAsync(() => ButtonLabeled(cut, "Ver bitácora en TV").Click());

        cut.WaitForAssertion(() => (Tv.Journal is not null, File.Exists(Vault.PathOf(JournalPaths.LogPath))).Should().Be((true, false)), Wait);
    }

    [Fact]
    public void Availability_needs_the_index_and_the_service()
    {
        var withBoth = JournalLogSection.CanShow(Services);
        var onlyIndex = JournalLogSection.CanShow(new ServiceCollection().AddSingleton(Journal).BuildServiceProvider());

        (withBoth, onlyIndex).Should().Be((true, false));
    }
}
