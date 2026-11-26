using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Journal;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalFormDialogTests : JournalUiTestBase
{
    private const string Perdida = TorvaldDiary + "/2026-11-12 Perdida.md";
    private const string Rara = TorvaldDiary + "/2026-11-13 Rara.md";

    public JournalFormDialogTests() => Services.AddSingleton<JournalStore>();

    protected override Task SeedAsync()
    {
        WriteFile("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n");
        WriteFile(Perdida, EntryText("2026-11-12T19:00:00+01:00", ["falta.png", "2026-11-12 Perdida-2.png"]));
        WriteImage(TorvaldDiary + "/2026-11-12 Perdida-2.png");
        WriteFile(Rara, EntryText("2026-11-13T19:00:00+01:00", ["raro.bmp", "2026-11-13 Rara-1.png", "2026-11-13 Rara-1.png"]));
        WriteImage(TorvaldDiary + "/2026-11-13 Rara-1.png");
        return Task.CompletedTask;
    }

    private JournalStore Store => Services.GetRequiredService<JournalStore>();

    private async Task<string> SeedEntryAsync(string title = "Ankheg", params string[] images)
    {
        var result = await Store.CreateAsync(new JournalCreate(Lyra, title, "Nota.", null,
            [.. (images.Length == 0 ? ["uno.png", "dos.jpg"] : images).Select(name => new JournalImageSource(name, () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))))]));
        return result.EntryPath!;
    }

    // El primer render con miniaturas lanza el listado de imágenes y su aviso repinta: se espera antes para que los manejadores no caduquen.
    private async Task<IRenderedComponent<JournalFormDialog>> OpenAsync(JournalFormRequest request)
    {
        await Journal.RefreshImagesAsync();
        var cut = Render<JournalFormDialog>();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(request));
        cut.WaitForAssertion(() => cut.Find(".ds-dialog-backdrop").HasAttribute("hidden").Should().BeFalse(), Wait);
        return cut;
    }

    private static IElement ButtonLabeled(IRenderedComponent<JournalFormDialog> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == label);

    private static IReadOnlyList<string> ImageNames(IRenderedComponent<JournalFormDialog> cut) =>
        [.. cut.FindAll(".journal-form__image-name").Select(e => e.TextContent.Trim())];

    private static string SelectedPc(IRenderedComponent<JournalFormDialog> cut) => cut.Find("select option[selected]").TextContent;

    private string[] Notes(string folder) =>
        [.. Directory.GetFiles(Vault.PathOf(folder), "*.md").Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal)];

    private static async Task UploadAsync(IRenderedComponent<JournalFormDialog> cut, params (string Name, byte Byte)[] files) =>
        await cut.InvokeAsync(() => cut.FindComponent<InputFile>().UploadFiles(
            [.. files.Select(f => InputFileContent.CreateFromBinary([f.Byte, 2, 3], f.Name))]));

    [Fact]
    public async Task A_new_form_proposes_the_pc_of_the_request()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Torvald));

        SelectedPc(cut).Should().Be("Torvald Martillo de Sal");
    }

    [Fact]
    public async Task A_new_form_without_a_pc_proposes_the_last_one_used_in_the_session()
    {
        Shell.RememberJournalPc(Torvald);

        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New));

        SelectedPc(cut).Should().Be("Torvald Martillo de Sal");
    }

    [Fact]
    public async Task A_new_form_whose_last_pc_no_longer_exists_proposes_the_first_one()
    {
        Shell.RememberJournalPc("Personajes/Borrado.md");

        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New));

        SelectedPc(cut).Should().Be("Lyra Vientoalto");
    }

    [Fact]
    public async Task The_title_of_the_dialog_depends_on_the_mode()
    {
        var path = await SeedEntryAsync();

        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New));
        var newTitle = cut.Find("h2").TextContent;
        await cut.InvokeAsync(Shell.CloseJournalForm);
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.Edit, null, path)));

        cut.WaitForAssertion(() => (newTitle, cut.Find("h2").TextContent).Should().Be(("Nueva entrada", "Editar entrada")), Wait);
    }

    [Fact]
    public async Task Save_is_disabled_until_there_is_an_image()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        var before = ButtonLabeled(cut, "Guardar").HasAttribute("disabled");

        await UploadAsync(cut, ("uno.png", 1));

        (before, ButtonLabeled(cut, "Guardar").HasAttribute("disabled")).Should().Be((true, false));
    }

    [Fact]
    public async Task Added_images_are_listed_by_name_in_order()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));

        await UploadAsync(cut, ("uno.png", 1), ("dos.jpg", 2));

        ImageNames(cut).Should().Equal("uno.png", "dos.jpg");
    }

    [Fact]
    public async Task Saving_with_the_browser_file_input_writes_the_note_and_the_images_in_the_pc_diary()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("uno.png", 1), ("dos.JPG", 2));
        cut.Find("input[type=text]").Input("Ankheg");
        cut.Find("textarea").Input("Salió del suelo.");

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        var entry = Journal.ForPc(Lyra).Single();
        (entry.Title, entry.Note, string.Join(",", entry.Images.Select(i => Path.GetExtension(i.Name))), File.ReadAllBytes(Vault.PathOf(entry.Images[1].Path))[0])
            .Should().Be(("Ankheg", "Salió del suelo.", ".png,.jpg", (byte)2));
    }

    [Fact]
    public async Task After_saving_the_entry_opens_in_the_diary_tab_of_its_pc_and_the_pc_is_remembered()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Torvald));
        await UploadAsync(cut, ("uno.png", 1));
        cut.Find("input[type=text]").Input("Faro");

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.SelectedJournalPath.Should().NotBeNull(), Wait);
        (Shell.ActivePanel, Shell.SelectedPcPath, Shell.PlayerTab, Shell.SelectedJournalPath!.EndsWith(" Faro.md", StringComparison.Ordinal), Shell.LastJournalPcPath)
            .Should().Be((OverlayPanel.Players, Torvald, PlayerTab.Journal, true, Torvald));
    }

    [Fact]
    public async Task A_file_that_is_not_an_image_is_discarded_with_the_notice_and_the_rest_is_saved()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("nota.pdf", 1), ("uno.png", 2));
        var warning = cut.Markup.Contains("No se pudo leer 1 imagen(es)", StringComparison.Ordinal);

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        (warning, ImageCount(), Shell.JournalNotice).Should().Be((true, 1, "No se pudo leer 1 imagen(es)"));

        int ImageCount() => Journal.ForPc(Lyra).Single().Images.Count;
    }

    [Fact]
    public async Task Images_can_be_moved_and_removed_before_saving()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("a.png", 1), ("b.png", 2), ("c.png", 3));

        cut.FindAll("li.journal-form__image")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Bajar").Click();
        cut.FindAll("li.journal-form__image")[2].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Quitar").Click();
        var order = string.Join(",", ImageNames(cut));
        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        var saved = Journal.ForPc(Lyra).Single();
        (order, saved.Images.Count, File.ReadAllBytes(Vault.PathOf(saved.Images[0].Path))[0], File.ReadAllBytes(Vault.PathOf(saved.Images[1].Path))[0])
            .Should().Be(("b.png,a.png", 2, (byte)2, (byte)1));
    }

    [Fact]
    public async Task The_last_image_cannot_be_removed()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("a.png", 1));

        cut.Find("li.journal-form__image").QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Quitar").HasAttribute("disabled")
            .Should().BeTrue();
    }

    [Fact]
    public async Task Discard_closes_the_form_and_saves_nothing()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("a.png", 1));

        ButtonLabeled(cut, "Descartar").Click();

        cut.WaitForAssertion(() => (Shell.JournalForm, Directory.Exists(Vault.PathOf(LyraDiary))).Should().Be((null, false)), Wait);
    }

    [Fact]
    public async Task A_new_request_starts_with_an_empty_form()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("a.png", 1));
        cut.Find("input[type=text]").Input("Borrador");

        await cut.InvokeAsync(Shell.CloseJournalForm);
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Lyra)));

        (ImageNames(cut).Count, cut.Find("input[type=text]").GetAttribute("value") ?? string.Empty).Should().Be((0, string.Empty));
    }

    [Fact]
    public async Task Choosing_a_creature_from_the_glossary_saves_a_link_to_the_note()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("a.png", 1));

        cut.Find("input[type=search]").Input("Lobo");
        cut.WaitForAssertion(() => cut.FindAll("button.ds-list-row").Count.Should().BeGreaterThan(0), Wait);
        cut.FindAll("button.ds-list-row").First(b => b.TextContent.Contains("Lobo", StringComparison.Ordinal)).Click();
        var chip = cut.Markup.Contains("Quitar criatura", StringComparison.Ordinal);
        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        (chip, Journal.ForPc(Lyra).Single().Creature).Should().Be((true, "[[Lobo]]"));
    }

    [Fact]
    public async Task Removing_the_creature_goes_back_to_the_search()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        cut.Find("input[type=search]").Input("Lobo");
        cut.WaitForAssertion(() => cut.FindAll("button.ds-list-row").Count.Should().BeGreaterThan(0), Wait);
        cut.FindAll("button.ds-list-row").First(b => b.TextContent.Contains("Lobo", StringComparison.Ordinal)).Click();

        ButtonLabeled(cut, "Quitar criatura").Click();

        cut.FindAll("input[type=search]").Count.Should().Be(1);
    }

    [Fact]
    public async Task Editing_loads_the_entry_and_locks_the_pc()
    {
        var path = await SeedEntryAsync("Ankheg", "uno.png", "dos.jpg");

        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));

        (cut.Find("input[type=text]").GetAttribute("value"), cut.Find("textarea").GetAttribute("value"), ImageNames(cut).Count, cut.Find("select").HasAttribute("disabled"), SelectedPc(cut))
            .Should().Be(("Ankheg", "Nota.", 2, true, "Lyra Vientoalto"));
    }

    [Fact]
    public async Task Editing_shows_a_thumbnail_of_each_saved_image()
    {
        var path = await SeedEntryAsync();

        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));

        cut.FindAll("li.journal-form__image img").Count.Should().Be(2);
    }

    [Fact]
    public async Task Editing_the_title_renames_the_note_and_opens_it_under_its_new_name()
    {
        var path = await SeedEntryAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));

        cut.Find("input[type=text]").Input("Dragón");
        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        (Notes(LyraDiary).Single().EndsWith(" Dragón.md", StringComparison.Ordinal), Shell.SelectedJournalPath!.EndsWith(" Dragón.md", StringComparison.Ordinal),
                Journal.Get(Shell.SelectedJournalPath!)!.Images.Select(i => i.Name.Contains("Dragón", StringComparison.Ordinal)).All(x => x))
            .Should().Be((true, true, true));
    }

    [Fact]
    public async Task Removing_a_saved_image_asks_first_and_deletes_the_file_only_when_saving()
    {
        var path = await SeedEntryAsync();
        var removed = Journal.Get(path)!.Images[0];
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));

        cut.FindAll("li.journal-form__image")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Quitar").Click();
        var asked = cut.Markup.Contains($"¿Quitar la imagen «{removed.Name}»?", StringComparison.Ordinal);
        var rowsWhileAsking = ImageNames(cut).Count;
        cut.FindAll(".ds-notice button").First(b => b.TextContent.Trim() == "Quitar").Click();
        var rowsAfterConfirming = ImageNames(cut).Count;
        var fileBeforeSaving = File.Exists(Vault.PathOf(removed.Path));
        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        (asked, rowsWhileAsking, rowsAfterConfirming, fileBeforeSaving, File.Exists(Vault.PathOf(removed.Path))).Should().Be((true, 2, 1, true, false));
    }

    [Fact]
    public async Task Cancelling_the_removal_keeps_the_image()
    {
        var path = await SeedEntryAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));
        cut.FindAll("li.journal-form__image")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Quitar").Click();

        cut.FindAll(".ds-notice button").First(b => b.TextContent.Trim() == "Cancelar").Click();

        (ImageNames(cut).Count, cut.FindAll(".ds-notice").Count).Should().Be((2, 0));
    }

    [Fact]
    public async Task A_note_changed_outside_the_app_is_reported_and_the_form_reloads_it()
    {
        var path = await SeedEntryAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));
        File.WriteAllText(Vault.PathOf(path), File.ReadAllText(Vault.PathOf(path)).Replace("Nota.", "Cambiada fuera.", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(Vault.PathOf(path), DateTime.UtcNow.AddMinutes(5));
        cut.Find("textarea").Input("Mi cambio");

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("La entrada cambió fuera de la app. Se ha vuelto a cargar"), Wait);
        (Shell.JournalForm is not null, File.ReadAllText(Vault.PathOf(path)).Contains("Cambiada fuera.", StringComparison.Ordinal)).Should().Be((true, true));
    }

    [Fact]
    public async Task Editing_an_entry_that_no_longer_exists_says_so()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, LyraDiary + "/2026-01-01 Nada.md"));

        (cut.Markup.Contains("La entrada ya no existe.", StringComparison.Ordinal), ButtonLabeled(cut, "Guardar").HasAttribute("disabled")).Should().Be((true, true));
    }

    [Fact]
    public async Task A_missing_image_is_left_out_of_the_draft_with_a_notice()
    {
        await Journal.RefreshImagesAsync();

        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, Perdida));

        (ImageNames(cut).Count, cut.Markup.Contains("ya no existen", StringComparison.Ordinal)).Should().Be((1, true));
    }

    [Fact]
    public async Task A_note_changed_outside_the_app_is_reloaded_from_disk_and_the_next_save_goes_through()
    {
        var path = await SeedEntryAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));
        File.WriteAllText(Vault.PathOf(path), File.ReadAllText(Vault.PathOf(path)).Replace("Nota.", "Cambiada fuera.", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(Vault.PathOf(path), DateTime.UtcNow.AddMinutes(5));
        ButtonLabeled(cut, "Guardar").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("La entrada cambió fuera de la app"), Wait);
        var reloaded = cut.Find("textarea").GetAttribute("value");

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        (reloaded, File.ReadAllText(Vault.PathOf(Shell.SelectedJournalPath!)).Contains("Cambiada fuera.", StringComparison.Ordinal)).Should().Be(("Cambiada fuera.", true));
    }

    private static Task EscapeAsync(IRenderedComponent<JournalFormDialog> cut) =>
        cut.InvokeAsync(() => cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" }));

    [Fact]
    public async Task Escape_on_a_form_without_changes_closes_it()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));

        await EscapeAsync(cut);

        Shell.JournalForm.Should().BeNull();
    }

    [Fact]
    public async Task Escape_with_text_asks_before_discarding_and_Seguir_editando_keeps_everything()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        cut.Find("input[type=text]").Input("Borrador");

        await EscapeAsync(cut);
        var asked = (Shell.JournalForm is not null, Shell.ConfirmingJournalDiscard);
        cut.FindAll(".ds-dialog__footer button").First(b => b.TextContent.Trim() == "Seguir editando").Click();

        (asked, Shell.JournalForm is not null, Shell.ConfirmingJournalDiscard, cut.Find("input[type=text]").GetAttribute("value"))
            .Should().Be(((true, true), true, false, "Borrador"));
    }

    [Fact]
    public async Task Confirming_the_question_discards_the_form()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("a.png", 1));
        await EscapeAsync(cut);

        cut.FindAll(".ds-dialog__footer button").First(b => b.TextContent.Trim() == "Descartar cambios").Click();

        cut.WaitForAssertion(() => (Shell.JournalForm, Shell.ConfirmingJournalDiscard, Directory.Exists(Vault.PathOf(LyraDiary))).Should().Be((null, false, false)), Wait);
    }

    [Fact]
    public async Task Editing_without_touching_anything_is_not_a_change_and_editing_the_note_is()
    {
        var path = await SeedEntryAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));
        var untouched = Shell.IsJournalFormDirty;

        cut.Find("textarea").Input("Otra nota");

        (untouched, Shell.IsJournalFormDirty).Should().Be((false, true));
    }

    [Fact]
    public async Task Moving_an_image_in_an_edit_is_a_change()
    {
        var path = await SeedEntryAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, path));

        cut.FindAll("li.journal-form__image")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Bajar").Click();

        Shell.IsJournalFormDirty.Should().BeTrue();
    }

    [Fact]
    public async Task Images_with_an_unsupported_or_repeated_name_are_left_out_of_the_draft_with_a_notice_and_the_entry_can_be_saved()
    {
        await Journal.RefreshImagesAsync();
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.Edit, null, Rara));
        var shown = (ImageNames(cut).Count, cut.Markup.Contains("nombre no admitido o repetido", StringComparison.Ordinal));

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        (shown, Journal.Get(Rara)?.Images.Count ?? Journal.Get(Shell.SelectedJournalPath!)!.Images.Count).Should().Be(((1, true), 1));
    }

    [Fact]
    public async Task Adding_images_in_two_batches_saves_all_of_them()
    {
        var cut = await OpenAsync(new JournalFormRequest(JournalFormMode.New, Lyra));
        await UploadAsync(cut, ("uno.png", 1));
        await UploadAsync(cut, ("dos.png", 2));

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        Journal.ForPc(Lyra).Single().Images.Count.Should().Be(2);
    }

    [Fact]
    public void Availability_needs_the_index_and_the_store()
    {
        var withBoth = JournalFormDialog.CanEdit(Services);
        var onlyIndex = JournalFormDialog.CanEdit(new ServiceCollection().AddSingleton(Journal).BuildServiceProvider());

        (withBoth, onlyIndex).Should().Be((true, false));
    }
}
