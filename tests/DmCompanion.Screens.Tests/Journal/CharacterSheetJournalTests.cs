using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class CharacterSheetJournalTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";

    public CharacterSheetJournalTests()
    {
        Services.AddSoundForTests();
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
    }

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"]));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        return Task.CompletedTask;
    }

    // La pestaña Diario lanza el listado de imágenes al pintarse y su aviso repinta: se espera antes para que los manejadores no caduquen.
    private async Task<IRenderedComponent<CharacterSheet>> RenderSheetAsync()
    {
        await Journal.RefreshImagesAsync();
        return Render<CharacterSheet>(p => p.Add(c => c.Entity, Host.Current!.Index.Get(Lyra)!));
    }

    private static string Tab(IRenderedComponent<CharacterSheet> cut, string label) =>
        cut.FindAll("[role=tab]").First(t => t.TextContent.Trim() == label).GetAttribute("aria-selected")!;

    [Fact]
    public async Task The_sheet_has_the_tabs_Ficha_and_Diario_with_Ficha_selected()
    {
        var cut = await RenderSheetAsync();

        (string.Join(",", cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim())), Tab(cut, "Ficha"), Tab(cut, "Diario")).Should().Be(("Ficha,Diario", "true", "false"));
    }

    [Fact]
    public async Task Ficha_shows_the_stats_and_not_the_diary()
    {
        var cut = await RenderSheetAsync();

        (cut.FindAll(".ds-stat").Count > 0, cut.FindAll(".journal-tab").Count).Should().Be((true, 0));
    }

    [Fact]
    public async Task Diario_replaces_the_sheet_with_the_entries_of_the_pc()
    {
        var cut = await RenderSheetAsync();

        await cut.InvokeAsync(() => cut.FindAll("[role=tab]").First(t => t.TextContent.Trim() == "Diario").Click());

        (Shell.PlayerTab, cut.FindAll(".ds-stat").Count, cut.FindAll(".journal-row").Count, Tab(cut, "Diario")).Should().Be((PlayerTab.Journal, 0, 1, "true"));
    }

    [Fact]
    public async Task The_portrait_and_the_name_stay_above_both_tabs()
    {
        var cut = await RenderSheetAsync();
        await cut.InvokeAsync(() => cut.FindAll("[role=tab]").First(t => t.TextContent.Trim() == "Diario").Click());

        cut.Find("[data-testid=pc-title]").TextContent.Should().Be("Lyra Vientoalto");
    }

    [Fact]
    public async Task Going_back_to_Ficha_shows_the_stats_again()
    {
        var cut = await RenderSheetAsync();
        await cut.InvokeAsync(() => cut.FindAll("[role=tab]").First(t => t.TextContent.Trim() == "Diario").Click());

        await cut.InvokeAsync(() => cut.FindAll("[role=tab]").First(t => t.TextContent.Trim() == "Ficha").Click());

        (cut.FindAll(".ds-stat").Count > 0, cut.FindAll(".journal-tab").Count).Should().Be((true, 0));
    }

    [Fact]
    public async Task An_entry_opened_from_elsewhere_lands_on_the_diary_tab_with_its_detail()
    {
        Shell.OpenJournalEntry(Ankheg, Lyra);

        var cut = await RenderSheetAsync();

        (Tab(cut, "Diario"), cut.Find("article.journal-detail h3").TextContent).Should().Be(("true", "Ankheg"));
    }
}
