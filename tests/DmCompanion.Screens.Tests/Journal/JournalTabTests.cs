using AngleSharp.Dom;
using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class JournalTabTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Lobo = LyraDiary + "/2026-11-10 Lobo.md";

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg", "2026-11-16 Ankheg-2.jpg"], note: "Salió del suelo."));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-2.jpg");
        WriteFile(Lobo, EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"], "[[Lobo]]"));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        WriteFile("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n");
        return Task.CompletedTask;
    }

    // El primer render lanza el listado de imágenes y su aviso repinta: se espera antes para que los manejadores no caduquen.
    private async Task<IRenderedComponent<JournalTab>> RenderTabAsync(string pc = Lyra)
    {
        await Journal.RefreshImagesAsync();
        return Render<JournalTab>(p => p.Add(c => c.Pc, Host.Current!.Index.Get(pc)!));
    }

    private static IElement Button(IRenderedComponent<JournalTab> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(text, StringComparison.Ordinal));

    [Fact]
    public async Task The_entries_of_the_pc_are_listed_newest_first()
    {
        var cut = await RenderTabAsync();

        string.Join(",", cut.FindAll(".journal-row__title").Select(e => e.TextContent)).Should().Be("Ankheg,Lobo");
    }

    [Fact]
    public async Task Each_row_shows_the_date_and_the_number_of_images()
    {
        var meta = (await RenderTabAsync()).FindAll(".journal-row__meta").Select(e => e.TextContent).ToArray();

        meta.Should().Equal("16/11/2026 · 2 imágenes", "10/11/2026 · 1 imagen");
    }

    [Fact]
    public async Task Each_row_has_a_thumbnail_of_its_first_image()
    {
        var cut = await RenderTabAsync();

        cut.FindAll(".journal-row .journal-thumb img").Count.Should().Be(2);
    }

    [Fact]
    public async Task A_row_shows_a_chip_with_the_linked_creature()
    {
        var rows = (await RenderTabAsync()).FindAll(".journal-row");

        (rows[0].QuerySelectorAll(".ds-condition").Length, rows[1].QuerySelector(".ds-condition")!.TextContent.Trim()).Should().Be((0, "Lobo"));
    }

    [Fact]
    public async Task A_pc_without_entries_shows_the_empty_state()
    {
        var cut = await RenderTabAsync(Torvald);

        (cut.Find(".ds-empty__title").TextContent, cut.FindAll(".journal-row").Count).Should().Be(("Sin entradas", 0));
    }

    [Fact]
    public async Task Nueva_entrada_opens_the_form_for_this_pc()
    {
        var cut = await RenderTabAsync(Torvald);

        Button(cut, "Nueva entrada").Click();

        Shell.JournalForm.Should().Be(new JournalFormRequest(JournalFormMode.New, Torvald));
    }

    [Fact]
    public async Task Touching_a_row_opens_its_detail_and_Atras_goes_back()
    {
        var cut = await RenderTabAsync();

        cut.FindAll(".journal-row__text")[0].Click();
        var open = (Shell.SelectedJournalPath, cut.FindAll("article.journal-detail").Count, cut.FindAll(".journal-row").Count);
        Button(cut, "Atrás").Click();

        (open, Shell.SelectedJournalPath, cut.FindAll(".journal-row").Count).Should().Be(((Ankheg, 1, 0), (string?)null, 2));
    }

    [Fact]
    public async Task Touching_the_thumbnail_also_opens_the_entry()
    {
        var cut = await RenderTabAsync();

        cut.FindAll(".journal-row .journal-thumb")[1].Click();

        Shell.SelectedJournalPath.Should().Be(Lobo);
    }

    [Fact]
    public async Task An_entry_opened_from_elsewhere_shows_its_detail()
    {
        Shell.OpenJournalEntry(Lobo, Lyra);

        var cut = await RenderTabAsync();

        cut.Find("article.journal-detail h3").TextContent.Should().Be("Lobo");
    }

    [Fact]
    public async Task The_notice_is_shown_and_can_be_closed()
    {
        Shell.SetJournalNotice("No se pudo leer 2 imagen(es)");
        var cut = await RenderTabAsync();
        var shown = cut.Markup.Contains("No se pudo leer 2 imagen(es)", StringComparison.Ordinal);

        cut.Find("button[aria-label='Cerrar aviso']").Click();

        (shown, Shell.JournalNotice).Should().Be((true, (string?)null));
    }
}
