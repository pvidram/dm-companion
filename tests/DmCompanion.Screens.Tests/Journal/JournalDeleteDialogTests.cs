using AngleSharp.Dom;
using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalDeleteDialogTests : JournalUiTestBase
{
    public JournalDeleteDialogTests() => Services.AddSingleton<JournalStore>();

    private async Task<string> SeedEntryAsync(string title, params string[] images)
    {
        var result = await Services.GetRequiredService<JournalStore>().CreateAsync(new JournalCreate(Lyra, title, null, null,
            [.. images.Select(name => new JournalImageSource(name, () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))))]));
        return result.EntryPath!;
    }

    private async Task<IRenderedComponent<JournalDeleteDialog>> RenderDialogAsync(string path)
    {
        var cut = Render<JournalDeleteDialog>();
        await cut.InvokeAsync(() => Shell.RequestDeleteJournalEntry(path));
        return cut;
    }

    private static IElement Footer(IRenderedComponent<JournalDeleteDialog> cut, string text) =>
        cut.FindAll(".ds-dialog__footer button").First(b => b.TextContent.Trim() == text);

    private string[] FilesInDiary() => Directory.GetFiles(Vault.PathOf(LyraDiary)).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task The_message_names_the_entry_and_counts_its_images()
    {
        var path = await SeedEntryAsync("Ankheg", "a.png", "b.png");

        var cut = await RenderDialogAsync(path);

        cut.Find(".ds-dialog__body").TextContent.Should().Be("¿Borrar la entrada «Ankheg» y sus 2 imágenes?");
    }

    [Fact]
    public async Task The_message_for_one_image_is_singular()
    {
        var path = await SeedEntryAsync("Faro", "a.png");

        var cut = await RenderDialogAsync(path);

        cut.Find(".ds-dialog__body").TextContent.Should().Be("¿Borrar la entrada «Faro» y su imagen?");
    }

    [Fact]
    public async Task It_is_closed_when_nothing_is_being_deleted()
    {
        await SeedEntryAsync("Ankheg", "a.png");

        Render<JournalDeleteDialog>().Find(".ds-dialog-backdrop").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public async Task Borrar_deletes_the_note_and_its_images_and_closes_the_entry()
    {
        var path = await SeedEntryAsync("Ankheg", "a.png", "b.png");
        await SeedEntryAsync("Otra", "c.png");
        Shell.OpenJournalEntry(path, Lyra);
        var cut = await RenderDialogAsync(path);

        Footer(cut, "Borrar").Click();

        cut.WaitForAssertion(() => (Shell.DeletingJournalPath, Shell.SelectedJournalPath, FilesInDiary().Length).Should().Be(((string?)null, (string?)null, 2)), Wait);
    }

    [Fact]
    public async Task Cancelar_keeps_everything()
    {
        var path = await SeedEntryAsync("Ankheg", "a.png");
        var cut = await RenderDialogAsync(path);

        Footer(cut, "Cancelar").Click();

        cut.WaitForAssertion(() => (Shell.DeletingJournalPath, FilesInDiary().Length).Should().Be(((string?)null, 2)), Wait);
    }

    [Fact]
    public async Task A_note_changed_outside_the_app_is_not_deleted_and_the_dm_is_told()
    {
        var path = await SeedEntryAsync("Ankheg", "a.png");
        var cut = await RenderDialogAsync(path);
        File.SetLastWriteTimeUtc(Vault.PathOf(path), DateTime.UtcNow.AddMinutes(5));

        Footer(cut, "Borrar").Click();

        cut.WaitForAssertion(() => (Shell.JournalNotice, FilesInDiary().Length).Should().Be(("La entrada cambió fuera de la app y no se ha borrado.", 2)), Wait);
    }
}
