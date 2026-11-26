using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Journal;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components.Web;

namespace DmCompanion.Screens.Tests;

public sealed class JournalEntryDetailTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Lobo = LyraDiary + "/2026-11-10 Lobo.md";
    private const string Perdida = LyraDiary + "/2026-11-12 Perdida.md";
    private const string Foto = LyraDiary + "/2026-11-13 Foto.md";
    private const string Rota = LyraDiary + "/2026-11-14 Rota.md";
    private const string Html = LyraDiary + "/2026-11-15 Html.md";

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg", "2026-11-16 Ankheg-2.jpg"], note: "Salió del suelo."));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-2.jpg");
        WriteFile(Lobo, EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"], "[[Lobo]]"));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        WriteFile("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n");
        WriteFile(Perdida, EntryText("2026-11-12T19:00:00+01:00", ["falta.png", "2026-11-12 Perdida-2.png"]));
        WriteImage(LyraDiary + "/2026-11-12 Perdida-2.png");
        WriteFile(Foto, EntryText("2026-11-13T19:00:00+01:00", ["foto.heic"]));
        WriteImage(LyraDiary + "/foto.heic");
        WriteFile(Rota, "---\ntype: journal\nimages: [sin cerrar\n---\nHola\n");
        WriteFile(Html, EntryText("2026-11-15T19:00:00+01:00", ["2026-11-15 Html-1.png"], note: "<b>negrita</b>\nLínea 2"));
        WriteImage(LyraDiary + "/2026-11-15 Html-1.png");
        return Task.CompletedTask;
    }

    // El primer render lanza el listado de imágenes y su aviso repinta: se espera antes para que los manejadores no caduquen.
    private async Task<IRenderedComponent<JournalEntryDetail>> RenderDetailAsync(string path)
    {
        await Journal.RefreshImagesAsync();
        return Render<JournalEntryDetail>(p => p.Add(c => c.Entry, Journal.Get(path)!));
    }

    private static IElement Button(IRenderedComponent<JournalEntryDetail> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(text, StringComparison.Ordinal));

    private static string Src(IRenderedComponent<JournalEntryDetail> cut) => cut.Find(".journal-viewer__image").GetAttribute("src")!;

    private static TouchEventArgs Touch(double x) => new() { ChangedTouches = [new TouchPoint { ClientX = x }] };

    [Fact]
    public async Task It_shows_the_title_the_owner_the_date_and_the_image_count()
    {
        var cut = await RenderDetailAsync(Ankheg);

        (cut.Find("h3").TextContent, cut.Find(".journal-detail__meta").TextContent).Should().Be(("Ankheg", "Lyra · 16/11/2026 · 2 imágenes"));
    }

    [Fact]
    public async Task It_shows_the_first_image_big_and_Siguiente_the_next()
    {
        var cut = await RenderDetailAsync(Ankheg);
        var first = Src(cut);

        Button(cut, "Siguiente").Click();

        (first.Contains("Ankheg-1.jpg", StringComparison.Ordinal), Src(cut).Contains("Ankheg-2.jpg", StringComparison.Ordinal), cut.Find(".journal-viewer__bar span").TextContent)
            .Should().Be((true, true, "Imagen 2 de 2"));
    }

    [Fact]
    public async Task The_buttons_are_disabled_at_each_end()
    {
        var cut = await RenderDetailAsync(Ankheg);
        var atStart = (Button(cut, "Anterior").HasAttribute("disabled"), Button(cut, "Siguiente").HasAttribute("disabled"));

        Button(cut, "Siguiente").Click();

        (atStart, (Button(cut, "Anterior").HasAttribute("disabled"), Button(cut, "Siguiente").HasAttribute("disabled")))
            .Should().Be(((true, false), (false, true)));
    }

    [Fact]
    public async Task Swiping_left_shows_the_next_image_and_right_the_previous_one()
    {
        var cut = await RenderDetailAsync(Ankheg);

        cut.Find(".journal-viewer").TouchStart(Touch(300));
        cut.Find(".journal-viewer").TouchEnd(Touch(100));
        var afterLeft = Src(cut).Contains("Ankheg-2.jpg", StringComparison.Ordinal);
        cut.Find(".journal-viewer").TouchStart(Touch(100));
        cut.Find(".journal-viewer").TouchEnd(Touch(300));

        (afterLeft, Src(cut).Contains("Ankheg-1.jpg", StringComparison.Ordinal)).Should().Be((true, true));
    }

    [Fact]
    public async Task A_short_drag_does_not_change_the_image()
    {
        var cut = await RenderDetailAsync(Ankheg);

        cut.Find(".journal-viewer").TouchStart(Touch(300));
        cut.Find(".journal-viewer").TouchEnd(Touch(280));

        Src(cut).Should().Contain("Ankheg-1.jpg");
    }

    [Fact]
    public async Task A_missing_image_is_a_card_with_its_name_and_the_next_one_still_shows()
    {
        await Journal.RefreshImagesAsync();
        var cut = await RenderDetailAsync(Perdida);
        var card = cut.Find(".journal-viewer__card").TextContent;

        Button(cut, "Siguiente").Click();

        (card, Src(cut).Contains("Perdida-2.png", StringComparison.Ordinal)).Should().Be(("Imagen no encontrada: falta.png", true));
    }

    [Fact]
    public async Task A_missing_image_is_not_repeated_as_a_warning()
    {
        await Journal.RefreshImagesAsync();

        (await RenderDetailAsync(Perdida)).FindAll(".ds-notice").Count.Should().Be(0);
    }

    [Fact]
    public async Task A_heic_is_a_card_that_says_the_format_is_not_supported_and_gives_the_file_name()
    {
        var card = (await RenderDetailAsync(Foto)).Find(".journal-viewer__card");

        (card.QuerySelectorAll("p").Select(p => p.TextContent).ToArray()).Should().Equal("Formato no compatible", "foto.heic");
    }

    [Fact]
    public async Task The_note_is_shown_as_text_never_as_html()
    {
        var cut = await RenderDetailAsync(Html);

        (cut.Find(".journal-detail__note").TextContent, cut.FindAll(".journal-detail__note b").Count).Should().Be(("<b>negrita</b>\nLínea 2", 0));
    }

    [Fact]
    public async Task A_broken_frontmatter_is_shown_as_a_warning_with_what_there_is()
    {
        var cut = await RenderDetailAsync(Rota);

        (cut.Find(".ds-notice").TextContent.Contains("Frontmatter YAML inválido", StringComparison.Ordinal), cut.Find(".journal-detail__note").TextContent)
            .Should().Be((true, "Hola"));
    }

    [Fact]
    public async Task An_entry_with_invalid_yaml_has_neither_Editar_nor_Borrar_and_says_to_fix_it_in_Obsidian()
    {
        var cut = await RenderDetailAsync(Rota);

        (cut.FindAll(".journal-detail__actions button").Select(b => b.TextContent.Trim()).Contains("Borrar"), cut.FindAll(".journal-detail__actions button").Any(b => b.TextContent.Trim() == "Editar"),
                cut.Find(".journal-detail__actions").TextContent.Contains("Arregla el YAML en Obsidian para editarla o borrarla", StringComparison.Ordinal))
            .Should().Be((false, false, true));
    }

    [Fact]
    public async Task The_linked_creature_is_a_chip()
    {
        var cut = await RenderDetailAsync(Lobo);

        cut.Find(".journal-detail .ds-condition").TextContent.Trim().Should().Be("Lobo");
    }

    [Fact]
    public async Task Editar_opens_the_form_on_this_entry()
    {
        var cut = await RenderDetailAsync(Ankheg);

        Button(cut, "Editar").Click();

        Shell.JournalForm.Should().Be(new JournalFormRequest(JournalFormMode.Edit, null, Ankheg));
    }

    [Fact]
    public async Task Borrar_asks_for_confirmation_of_this_entry()
    {
        var cut = await RenderDetailAsync(Ankheg);

        Button(cut, "Borrar").Click();

        Shell.DeletingJournalPath.Should().Be(Ankheg);
    }

    [Fact]
    public async Task Without_a_tv_the_actions_are_only_editar_and_borrar_besides_the_navigation()
    {
        var cut = await RenderDetailAsync(Ankheg);

        cut.FindAll(".journal-detail__actions button").Select(b => b.TextContent.Trim()).Should().Equal("Editar", "Borrar");
    }

    [Fact]
    public async Task Changing_the_entry_goes_back_to_its_first_image()
    {
        var cut = await RenderDetailAsync(Ankheg);
        Button(cut, "Siguiente").Click();

        cut.Render(p => p.Add(c => c.Entry, Journal.Get(Lobo)!));

        (Src(cut).Contains("Lobo-1.png", StringComparison.Ordinal), cut.Find(".journal-viewer__bar span").TextContent).Should().Be((true, "Imagen 1 de 1"));
    }
}
