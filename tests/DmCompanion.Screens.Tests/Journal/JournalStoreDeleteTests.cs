using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class JournalStoreDeleteTests : JournalStoreTestBase
{
    private const string Entry = Diary + "/2026-11-16 Ankheg.md";

    [Fact]
    public async Task Delete_removes_the_note_and_its_images_and_nothing_else()
    {
        await CreateAsync("Ankheg", "a.png", "b.jpg");
        await CreateAsync("Otra", "c.png");

        var result = await Store.DeleteAsync(Entry, await StampAsync(Entry));

        (result.Status, Names(Diary)).Should().Be((JournalStatus.Done, "2026-11-16 Otra-1.png|2026-11-16 Otra.md"));
    }

    [Fact]
    public async Task Delete_forgets_the_note_in_the_index()
    {
        await CreateAsync("Ankheg", "a.png");

        await Store.DeleteAsync(Entry, await StampAsync(Entry));

        Host.Current!.Index.Get(Entry).Should().BeNull();
    }

    [Fact]
    public async Task Delete_with_a_note_changed_on_disk_deletes_nothing()
    {
        await CreateAsync("Ankheg", "a.png");
        var stamp = await StampAsync(Entry);
        File.SetLastWriteTimeUtc(PathOf(Entry), stamp.AddMinutes(5));

        var result = await Store.DeleteAsync(Entry, stamp);

        (result.Status, Names(Diary)).Should().Be((JournalStatus.ModifiedOnDisk, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Delete_of_a_missing_note_is_NotFound()
    {
        var result = await Store.DeleteAsync(Entry, DateTime.UtcNow);

        result.Status.Should().Be(JournalStatus.NotFound);
    }

    [Fact]
    public async Task Delete_never_removes_a_note_that_is_not_a_journal_entry()
    {
        Directory.CreateDirectory(PathOf(Diary));
        File.WriteAllText(PathOf(Diary + "/Suelta.md"), "---\ntype: note\n---\nMía.\n");

        var result = await Store.DeleteAsync(Diary + "/Suelta.md", await StampAsync(Diary + "/Suelta.md"));

        (result.Status, Exists(Diary + "/Suelta.md")).Should().Be((JournalStatus.Failed, true));
    }

    [Fact]
    public async Task Delete_never_removes_a_note_outside_a_Diario_folder()
    {
        var result = await Store.DeleteAsync("Personajes/Lyra.md", await StampAsync("Personajes/Lyra.md"));

        (result.Status, Exists("Personajes/Lyra.md")).Should().Be((JournalStatus.Failed, true));
    }

    [Fact]
    public async Task Delete_never_removes_a_file_that_is_not_an_image_even_if_the_note_lists_it()
    {
        await CreateAsync("Otra", "c.png");
        await CreateAsync("Ankheg", "a.png");
        File.WriteAllText(PathOf(Entry), File.ReadAllText(PathOf(Entry)).Replace("images:\n", "images:\n  - \"2026-11-16 Otra.md\"\n", StringComparison.Ordinal));

        var result = await Store.DeleteAsync(Entry, await StampAsync(Entry));

        (result.Status, Exists(Diary + "/2026-11-16 Otra.md"), Exists(Entry)).Should().Be((JournalStatus.Done, true, false));
    }

    [Fact]
    public async Task Delete_keeps_an_image_that_another_entry_also_cites()
    {
        await CreateAsync("Ankheg", "a.png", "b.png");
        File.Copy(PathOf(Entry), PathOf(Diary + "/2026-11-16 Copia.md"));

        var result = await Store.DeleteAsync(Entry, await StampAsync(Entry));

        (result.Status, Names(Diary)).Should().Be((JournalStatus.Done, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg-2.png|2026-11-16 Copia.md"));
    }

    [Fact]
    public async Task Delete_keeps_every_image_when_a_sibling_note_has_broken_yaml()
    {
        await CreateAsync("Ankheg", "a.png", "b.png");
        File.WriteAllText(PathOf(Diary + "/2026-11-16 Copia.md"), "---\ntype: journal\nimages: [\n---\nA medio editar.\n");

        var result = await Store.DeleteAsync(Entry, await StampAsync(Entry));

        (result.Status, Names(Diary)).Should().Be((JournalStatus.Done, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg-2.png|2026-11-16 Copia.md"));
    }

    [Fact]
    public async Task Delete_ignores_a_valid_sibling_note_that_is_not_a_journal_entry()
    {
        await CreateAsync("Ankheg", "a.png");
        File.WriteAllText(PathOf(Diary + "/Suelta.md"), "---\ntype: note\n---\nMía.\n");

        var result = await Store.DeleteAsync(Entry, await StampAsync(Entry));

        (result.Status, Names(Diary)).Should().Be((JournalStatus.Done, "Suelta.md"));
    }

    [Fact]
    public async Task Delete_fails_without_touching_anything_when_a_sibling_note_cannot_be_read()
    {
        await CreateAsync("Ankheg", "a.png");
        File.WriteAllText(PathOf(Diary + "/Otra.md"), "---\ntype: journal\n---\n");
        var stamp = await StampAsync(Entry);
        Factory.Storage!.LockedNote = Diary + "/Otra.md";

        var result = await Store.DeleteAsync(Entry, stamp);

        (result.Status, Names(Diary)).Should().Be((JournalStatus.Failed, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg.md|Otra.md"));
    }
}
