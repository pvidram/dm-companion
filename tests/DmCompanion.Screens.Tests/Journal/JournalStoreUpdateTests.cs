using DmCompanion.Core.Journal;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class JournalStoreUpdateTests : JournalStoreTestBase
{
    private const string Entry = Diary + "/2026-11-16 Ankheg.md";
    private const string One = "2026-11-16 Ankheg-1.jpg";
    private const string Two = "2026-11-16 Ankheg-2.png";

    private async Task<DateTime> CreateTwoImagesAsync()
    {
        await CreateAsync("Ankheg", "uno.jpg", "dos.png");
        return await StampAsync(Entry);
    }

    private Task<JournalResult> UpdateAsync(DateTime stamp, string? title, string? note, string? creature, params JournalImageItem[] images) =>
        Store.UpdateAsync(new JournalEdit(Entry, stamp, title, note, creature, images));

    [Fact]
    public async Task Update_with_a_new_title_renames_the_note_and_the_images_keeping_the_date()
    {
        var stamp = await CreateTwoImagesAsync();
        Clock.Advance(TimeSpan.FromDays(3));

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        (result.Status, result.EntryPath, Names(Diary)).Should().Be(
            (JournalStatus.Done, Diary + "/2026-11-16 Dragón.md", "2026-11-16 Dragón-1.jpg|2026-11-16 Dragón-2.png|2026-11-16 Dragón.md"));
    }

    [Fact]
    public async Task Update_with_a_new_title_rewrites_the_image_names_in_the_note()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        var entry = JournalReader.Read(result.EntryPath!, File.ReadAllText(PathOf(result.EntryPath!)), DateTime.UtcNow, _ => true);
        string.Join(",", entry.Images.Select(i => i.Name)).Should().Be("2026-11-16 Dragón-1.jpg,2026-11-16 Dragón-2.png");
    }

    [Fact]
    public async Task Update_never_changes_the_added_date_and_keeps_extra_keys()
    {
        var stamp = await CreateTwoImagesAsync();
        File.WriteAllText(PathOf(Entry), File.ReadAllText(PathOf(Entry)).Replace("images:", "mood: tenso\nimages:", StringComparison.Ordinal));
        stamp = await StampAsync(Entry);
        Clock.Advance(TimeSpan.FromDays(3));

        var result = await UpdateAsync(stamp, "Ankheg", "Otra nota", null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        var text = File.ReadAllText(PathOf(result.EntryPath!));
        (text.Contains("added: 2026-11-16T21:34:00+01:00", StringComparison.Ordinal), text.Contains("mood: tenso", StringComparison.Ordinal), text.Contains("Otra nota", StringComparison.Ordinal))
            .Should().Be((true, true, true));
    }

    [Fact]
    public async Task Update_without_a_title_change_keeps_the_note_path_and_the_image_names()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Ankheg", "Nota nueva", "srd:criaturas/ankheg", JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        (result.EntryPath, Names(Diary)).Should().Be((Entry, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_adds_new_images_with_the_next_free_number_and_removes_the_dropped_ones()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Ankheg", null, null,
            JournalImageItem.Add(Image("tres.webp")), JournalImageItem.Keep(Two));

        var entry = JournalReader.Read(Entry, File.ReadAllText(PathOf(Entry)), DateTime.UtcNow, _ => true);
        (result.Status, string.Join(",", entry.Images.Select(i => i.Name)), Names(Diary)).Should().Be(
            (JournalStatus.Done, "2026-11-16 Ankheg-3.webp,2026-11-16 Ankheg-2.png", "2026-11-16 Ankheg-2.png|2026-11-16 Ankheg-3.webp|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_can_reorder_the_images_without_renaming_them()
    {
        var stamp = await CreateTwoImagesAsync();

        await UpdateAsync(stamp, "Ankheg", null, null, JournalImageItem.Keep(Two), JournalImageItem.Keep(One));

        var entry = JournalReader.Read(Entry, File.ReadAllText(PathOf(Entry)), DateTime.UtcNow, _ => true);
        string.Join(",", entry.Images.Select(i => i.Name)).Should().Be($"{Two},{One}");
    }

    [Fact]
    public async Task Update_with_a_title_that_collides_adds_2()
    {
        await CreateAsync("Dragón", "x.png");
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        result.EntryPath.Should().Be(Diary + "/2026-11-16 Dragón 2.md");
    }

    [Fact]
    public async Task Update_does_not_rename_when_only_the_case_of_the_title_changes()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "ANKHEG", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        (result.EntryPath, Names(Diary)).Should().Be((Entry, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_requires_at_least_one_image()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Ankheg", null, null);

        (result.Status, Names(Diary)).Should().Be((JournalStatus.NoImages, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_with_a_note_changed_on_disk_reports_it_and_changes_nothing()
    {
        var stamp = await CreateTwoImagesAsync();
        File.SetLastWriteTimeUtc(PathOf(Entry), stamp.AddMinutes(5));

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Add(Image("tres.png")));

        (result.Status, Names(Diary)).Should().Be((JournalStatus.ModifiedOnDisk, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_rolls_back_the_renames_and_the_new_images_when_the_note_cannot_be_written()
    {
        var stamp = await CreateTwoImagesAsync();
        var before = File.ReadAllText(PathOf(Entry));
        Factory.Storage!.FailNoteWrites = true;

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Add(Image("tres.png")));

        (result.Status, Names(Diary), File.ReadAllText(PathOf(Entry))).Should().Be(
            (JournalStatus.ModifiedOnDisk, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md", before));
    }

    [Fact]
    public async Task Update_rolls_back_without_renaming_when_the_note_cannot_be_written()
    {
        var stamp = await CreateTwoImagesAsync();
        Factory.Storage!.FailNoteWrites = true;

        var result = await UpdateAsync(stamp, "Ankheg", null, null, JournalImageItem.Keep(One), JournalImageItem.Add(Image("tres.png")), JournalImageItem.Keep(Two));

        (result.Status, Names(Diary)).Should().Be((JournalStatus.ModifiedOnDisk, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_never_rewrites_a_note_that_is_not_a_journal_entry()
    {
        Directory.CreateDirectory(PathOf(Diary));
        File.WriteAllText(PathOf(Diary + "/Suelta.md"), "---\ntype: note\n---\nMía.\n");
        var stamp = await StampAsync(Diary + "/Suelta.md");

        var result = await Store.UpdateAsync(new JournalEdit(Diary + "/Suelta.md", stamp, "Otra", null, null, [JournalImageItem.Add(Image("a.png"))]));

        (result.Status, File.ReadAllText(PathOf(Diary + "/Suelta.md"))).Should().Be((JournalStatus.Failed, "---\ntype: note\n---\nMía.\n"));
    }

    [Fact]
    public async Task Update_never_rewrites_a_note_with_invalid_yaml()
    {
        Directory.CreateDirectory(PathOf(Diary));
        const string broken = "---\ntype: journal\nimages: [sin cerrar\n---\nMía.\n";
        File.WriteAllText(PathOf(Diary + "/2026-11-16 Rota.md"), broken);
        var stamp = await StampAsync(Diary + "/2026-11-16 Rota.md");

        var result = await Store.UpdateAsync(new JournalEdit(Diary + "/2026-11-16 Rota.md", stamp, "Otra", null, null, [JournalImageItem.Add(Image("a.png"))]));

        (result.Status, File.ReadAllText(PathOf(Diary + "/2026-11-16 Rota.md"))).Should().Be((JournalStatus.Failed, broken));
    }

    [Fact]
    public async Task Update_refuses_a_note_outside_a_Diario_folder()
    {
        var stamp = await StampAsync("Personajes/Lyra.md");

        var result = await Store.UpdateAsync(new JournalEdit("Personajes/Lyra.md", stamp, "Otra", null, null, [JournalImageItem.Add(Image("a.png"))]));

        result.Status.Should().Be(JournalStatus.Failed);
    }

    [Fact]
    public async Task Update_of_a_missing_note_is_NotFound()
    {
        var result = await Store.UpdateAsync(new JournalEdit(Entry, DateTime.UtcNow, "x", null, null, [JournalImageItem.Add(Image("a.png"))]));

        result.Status.Should().Be(JournalStatus.NotFound);
    }

    [Fact]
    public async Task Update_refuses_to_keep_an_image_the_note_does_not_list()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Ankheg", null, null, JournalImageItem.Keep("../../Lyra.md"));

        result.Status.Should().Be(JournalStatus.Failed);
    }

    [Fact]
    public async Task Update_indexes_the_renamed_note_and_forgets_the_old_one()
    {
        var stamp = await CreateTwoImagesAsync();

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        (Host.Current!.Index.Get(result.EntryPath!) is not null, Host.Current.Index.Get(Entry) is null).Should().Be((true, true));
    }

    [Fact]
    public async Task Update_never_removes_a_file_that_is_not_an_image_even_if_the_note_lists_it()
    {
        await CreateAsync("Otra", "c.png");
        var stamp = await CreateTwoImagesAsync();
        File.WriteAllText(PathOf(Entry), File.ReadAllText(PathOf(Entry)).Replace("images:\n", "images:\n  - \"2026-11-16 Otra.md\"\n", StringComparison.Ordinal));
        stamp = await StampAsync(Entry);

        var result = await UpdateAsync(stamp, "Ankheg", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        (result.Status, Exists(Diary + "/2026-11-16 Otra.md")).Should().Be((JournalStatus.Done, true));
    }

    [Fact]
    public async Task Update_counts_a_new_image_whose_source_fails_as_unreadable_and_saves_the_rest()
    {
        var stamp = await CreateTwoImagesAsync();
        var broken = new JournalImageSource("x.png", () => throw new NotSupportedException());

        var result = await UpdateAsync(stamp, "Ankheg", null, null, JournalImageItem.Keep(One), JournalImageItem.Add(Image("tres.png")), JournalImageItem.Add(broken));

        (result.Status, result.Unreadable, Names(Diary)).Should().Be(
            (JournalStatus.Done, 1, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-3.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Update_renaming_does_not_move_an_image_that_another_entry_cites()
    {
        var stamp = await CreateTwoImagesAsync();
        File.Copy(PathOf(Entry), PathOf(Diary + "/2026-11-16 Copia.md"));

        var result = await UpdateAsync(stamp, "Dragón", null, null, JournalImageItem.Keep(One), JournalImageItem.Keep(Two));

        var note = File.ReadAllText(PathOf(result.EntryPath!));
        (result.Status, Names(Diary), note.Contains(One, StringComparison.Ordinal)).Should().Be(
            (JournalStatus.Done, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Copia.md|2026-11-16 Dragón.md", true));
    }

    [Fact]
    public async Task Update_removing_an_image_that_another_entry_cites_keeps_the_file()
    {
        var stamp = await CreateTwoImagesAsync();
        File.Copy(PathOf(Entry), PathOf(Diary + "/2026-11-16 Copia.md"));

        await UpdateAsync(stamp, null, null, null, JournalImageItem.Keep(One));

        Exists(Diary + "/" + Two).Should().BeTrue();
    }
}
