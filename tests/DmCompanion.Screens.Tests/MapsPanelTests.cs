using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class MapsPanelTests : MapsTestContext
{
    [Fact]
    public void Panel_lists_the_maps_by_name()
    {
        var cut = RenderOpenMaps();

        cut.FindAll("[data-testid=map-row]").Select(row => row.QuerySelector(".ds-list-row")!.TextContent.Trim())
            .Should().Equal("Cueva", "Faro");
    }

    [Fact]
    public void Panel_lists_orphan_images_under_their_own_heading()
    {
        var cut = RenderOpenMaps();

        (cut.Find("section.ds-parchment h3").TextContent, cut.Find("[data-testid=orphan-row]").TextContent)
            .Should().Match<(string, string)>(t => t.Item1 == "Imágenes sin mapa" && t.Item2.Contains("Mapas/suelto.png"));
    }

    [Fact]
    public void Rail_has_a_Mapas_button_that_opens_the_panel()
    {
        var cut = Render<DmShell>();

        cut.Find("button[aria-label='Mapas']").Click();

        Shell.ActivePanel.Should().Be(OverlayPanel.Maps);
    }

    [Fact]
    public void Selecting_a_map_opens_it_and_closes_the_panel()
    {
        var cut = RenderOpenMaps();

        cut.FindAll("[data-testid=map-row] .ds-list-row")[0].Click();

        cut.WaitForAssertion(() => (Session.Current?.NotePath, Shell.ActivePanel).Should().Be(("Mapas/Cueva.md", OverlayPanel.None)), Wait);
    }

    [Fact]
    public void Create_map_from_an_orphan_image_opens_the_new_map()
    {
        var cut = RenderOpenMaps();

        cut.Find("[data-testid=orphan-row] button").Click();

        cut.WaitForAssertion(() => Session.Current?.NotePath.Should().Be("Mapas/suelto.md"), Wait);
    }

    [Fact]
    public void Borrar_opens_a_destructive_confirmation_with_the_map_name()
    {
        var cut = RenderOpenMaps();

        cut.Find("button[aria-label='Borrar Cueva']").Click();

        cut.WaitForAssertion(() =>
        {
            OpenDialogText(cut).Should().Contain("«Cueva»");
            OpenDialog(cut).QuerySelector("button.ds-button--damage")!.TextContent.Should().Contain("Borrar");
        }, Wait);
    }

    [Fact]
    public void Cancelling_the_delete_confirmation_deletes_nothing()
    {
        var cut = RenderOpenMaps();
        cut.Find("button[aria-label='Borrar Cueva']").Click();
        cut.WaitForAssertion(() => OpenDialogText(cut).Should().Contain("«Cueva»"), Wait);

        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Cancelar").Click();

        cut.WaitForAssertion(() => (File.Exists(PathOf("Mapas/Cueva.md")), Shell.DeletingMapPath).Should().Be((true, null)), Wait);
    }

    [Fact]
    public void Confirming_the_delete_removes_the_map_from_disk_and_from_the_list()
    {
        var cut = RenderOpenMaps();
        cut.Find("button[aria-label='Borrar Cueva']").Click();
        cut.WaitForAssertion(() => OpenDialogText(cut).Should().Contain("«Cueva»"), Wait);

        OpenDialog(cut).QuerySelector("button.ds-button--damage")!.Click();

        cut.WaitForAssertion(() =>
            (File.Exists(PathOf("Mapas/Cueva.md")), cut.FindAll("[data-testid=map-row]").Count).Should().Be((false, 1)), Wait);
    }

    [Fact]
    public void Rename_dialog_warns_that_wikilinks_in_other_notes_are_not_updated()
    {
        var cut = RenderOpenMaps();

        cut.Find("button[aria-label='Renombrar Cueva']").Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector(".ds-notice")!.TextContent
            .Should().Match("*[[…]]*no se actualizan*"), Wait);
    }

    [Fact]
    public void Rename_dialog_starts_with_the_current_name()
    {
        var cut = RenderOpenMaps();

        cut.Find("button[aria-label='Renombrar Cueva']").Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=text]")!.GetAttribute("value").Should().Be("Cueva"), Wait);
    }

    [Fact]
    public void Accepting_the_rename_dialog_renames_the_files()
    {
        var cut = RenderOpenMaps();
        cut.Find("button[aria-label='Renombrar Cueva']").Click();
        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=text]")!.GetAttribute("value").Should().Be("Cueva"), Wait);
        OpenDialog(cut).QuerySelector("input[type=text]")!.Input("Gruta");
        // Un re-render tardío (recarga del catálogo, watcher) entre Input y Click puede retirar el manejador o pisar el valor: se espera a que el campo lo refleje.
        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=text]")!.GetAttribute("value").Should().Be("Gruta"), Wait);

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("button.ds-button--primary")!.Click(), Wait);

        cut.WaitForAssertion(() => File.Exists(PathOf("Mapas/Gruta.md")).Should().BeTrue(), Wait);
    }

    [Fact]
    public void Renaming_to_a_taken_name_keeps_the_dialog_open_with_an_error()
    {
        var cut = RenderOpenMaps();
        cut.Find("button[aria-label='Renombrar Cueva']").Click();
        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=text]")!.GetAttribute("value").Should().Be("Cueva"), Wait);
        OpenDialog(cut).QuerySelector("input[type=text]")!.Input("Faro");

        OpenDialog(cut).QuerySelector("button.ds-button--primary")!.Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector(".ds-notice--error")!.TextContent.Should().Contain("Ya existe"), Wait);
    }

    [Fact]
    public void New_map_dialog_creates_a_blank_map_and_opens_it()
    {
        var cut = RenderOpenMaps();
        cut.Find("section.ds-parchment .ds-parchment__actions button").Click();
        cut.WaitForAssertion(() => OpenDialogText(cut).Should().Contain("Nuevo mapa"), Wait);
        OpenDialog(cut).QuerySelector("input[type=text]")!.Input("Mazmorra");

        OpenDialog(cut).QuerySelector("button.ds-button--primary")!.Click();

        cut.WaitForAssertion(() => (Session.Current?.NotePath, Session.Current?.ImagePath).Should().Be(("Mapas/Mazmorra.md", null)), Wait);
    }

    [Fact]
    public void Import_dialog_without_a_native_picker_offers_an_image_input()
    {
        var cut = RenderOpenMaps();

        cut.FindAll("section.ds-parchment .ds-parchment__actions button")[1].Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=file]")!.GetAttribute("accept").Should().Be("image/*"), Wait);
    }

    [Fact]
    public void Import_dialog_suggests_the_file_name_without_extension()
    {
        var cut = RenderOpenMaps();
        cut.FindAll("section.ds-parchment .ds-parchment__actions button")[1].Click();
        cut.WaitForAssertion(() => cut.FindComponent<InputFile>(), Wait);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3 }, "Isla Perdida.png"));

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=text]")!.GetAttribute("value").Should().Be("Isla Perdida"), Wait);
    }

    [Fact]
    public void Import_dialog_writes_the_image_and_the_map_note()
    {
        var cut = RenderOpenMaps();
        cut.FindAll("section.ds-parchment .ds-parchment__actions button")[1].Click();
        cut.WaitForAssertion(() => cut.FindComponent<InputFile>(), Wait);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3 }, "Isla.png"));
        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("[data-testid=picked-image]").Should().NotBeNull(), Wait);

        OpenDialog(cut).QuerySelector("button.ds-button--primary")!.Click();

        cut.WaitForAssertion(() => (File.Exists(PathOf("Mapas/Isla.png")), File.Exists(PathOf("Mapas/Isla.md"))).Should().Be((true, true)), Wait);
    }

    [Fact]
    public void Import_dialog_without_an_image_asks_to_choose_one()
    {
        var cut = RenderOpenMaps();
        cut.FindAll("section.ds-parchment .ds-parchment__actions button")[1].Click();
        cut.WaitForAssertion(() => OpenDialogText(cut).Should().Contain("Importar imagen como mapa"), Wait);

        OpenDialog(cut).QuerySelector("button.ds-button--primary")!.Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector(".ds-notice--error")!.TextContent.Should().Contain("Elige una imagen"), Wait);
    }

    [Fact]
    public async Task Clicking_a_wikilink_to_a_map_opens_it_and_closes_the_notes_panel()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Shell.OpenNote("Historia/Pista.md"));
        cut.WaitForAssertion(() => cut.FindComponent<NoteBody>(), Wait);

        await cut.InvokeAsync(() => cut.FindComponent<NoteBody>().Instance.OnWikilinkClicked("Cueva", false));

        (Session.Current?.NotePath, Shell.ActivePanel).Should().Be(("Mapas/Cueva.md", OverlayPanel.None));
    }

    [Fact]
    public async Task Reading_a_map_note_shows_the_Abrir_mapa_button()
    {
        var cut = Render<DmShell>();

        await cut.InvokeAsync(() => Shell.OpenNote("Mapas/Cueva.md"));

        cut.WaitForAssertion(() => cut.Find("article.note-reader").QuerySelectorAll("button").Select(b => b.TextContent.Trim()).Should().Contain("Abrir mapa"), Wait);
    }

    [Fact]
    public async Task Reading_a_regular_note_does_not_show_the_Abrir_mapa_button()
    {
        var cut = Render<DmShell>();

        await cut.InvokeAsync(() => Shell.OpenNote("Historia/Pista.md"));

        cut.WaitForAssertion(() => cut.Find("article.note-reader").QuerySelectorAll("button").Select(b => b.TextContent.Trim()).Should().NotContain("Abrir mapa"), Wait);
    }

    [Fact]
    public async Task Abrir_mapa_button_opens_the_map_and_closes_the_notes_panel()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Shell.OpenNote("Mapas/Cueva.md"));
        cut.WaitForAssertion(() => cut.Find("article.note-reader"), Wait);

        cut.Find("article.note-reader").QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Abrir mapa").Click();

        cut.WaitForAssertion(() => (Session.Current?.NotePath, Shell.ActivePanel).Should().Be(("Mapas/Cueva.md", OverlayPanel.None)), Wait);
    }
}
