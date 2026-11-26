using DmCompanion.Core.Journal;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class JournalStoreCreateTests : JournalStoreTestBase
{
    private const string Entry = Diary + "/2026-11-16 Ankheg.md";

    [Fact]
    public async Task Create_writes_the_note_and_the_images_in_the_pc_diary()
    {
        var result = await CreateAsync("Ankheg", "uno.JPG", "dos.png");

        (result.Status, result.EntryPath, Names(Diary)).Should().Be(
            (JournalStatus.Done, Entry, "2026-11-16 Ankheg-1.jpg|2026-11-16 Ankheg-2.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Create_stores_the_images_byte_for_byte()
    {
        await Store.CreateAsync(new JournalCreate(Lyra, "Ankheg", null, null, [Image("a.png", 9, 8, 7)]));

        File.ReadAllBytes(PathOf(Diary + "/2026-11-16 Ankheg-1.png")).Should().Equal(9, 8, 7);
    }

    [Fact]
    public async Task Create_writes_a_note_that_reads_back_with_everything_the_dm_typed()
    {
        await Store.CreateAsync(new JournalCreate(Lyra, "Ankheg", "Salió del suelo.", "srd:criaturas/ankheg", [Image("a.png"), Image("b.webp")]));

        var entry = JournalReader.Read(Entry, File.ReadAllText(PathOf(Entry)), DateTime.UtcNow, _ => true);

        (entry.Added, string.Join(",", entry.Images.Select(i => i.Name)), entry.Creature, entry.Note).Should().Be(
            (new DateTimeOffset(2026, 11, 16, 21, 34, 0, TimeSpan.FromHours(1)), "2026-11-16 Ankheg-1.png,2026-11-16 Ankheg-2.webp", "srd:criaturas/ankheg", "Salió del suelo."));
    }

    [Fact]
    public async Task Create_without_title_uses_Entrada()
    {
        var result = await CreateAsync(null, "a.png");

        result.EntryPath.Should().Be(Diary + "/2026-11-16 Entrada.md");
    }

    [Fact]
    public async Task Create_with_a_taken_title_adds_2_to_the_note_and_the_images()
    {
        await CreateAsync("Ankheg", "a.png");

        var second = await CreateAsync("Ankheg", "b.png");

        (second.EntryPath, Names(Diary)).Should().Be((Diary + "/2026-11-16 Ankheg 2.md",
            "2026-11-16 Ankheg 2-1.png|2026-11-16 Ankheg 2.md|2026-11-16 Ankheg-1.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Create_replaces_invalid_characters_in_the_title_with_a_dash()
    {
        var result = await CreateAsync("¿Quién?", "a.png");

        result.EntryPath.Should().Be(Diary + "/2026-11-16 ¿Quién-.md");
    }

    [Fact]
    public async Task Create_discards_unsupported_files_and_counts_them()
    {
        var result = await CreateAsync("Ankheg", "a.png", "nota.pdf", "b.bmp");

        (result.Status, result.Unreadable, Names(Diary)).Should().Be((JournalStatus.Done, 2, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Create_discards_an_image_that_cannot_be_opened_and_keeps_the_rest()
    {
        var broken = new JournalImageSource("roto.png", () => throw new IOException("sin acceso"));

        var result = await Store.CreateAsync(new JournalCreate(Lyra, "Ankheg", null, null, [broken, Image("b.png")]));

        (result.Status, result.Unreadable, Names(Diary)).Should().Be((JournalStatus.Done, 1, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Create_with_nothing_readable_writes_nothing()
    {
        var result = await CreateAsync("Ankheg", "nota.pdf");

        (result.Status, result.EntryPath, Directory.Exists(PathOf(Diary))).Should().Be((JournalStatus.NoImages, null, false));
    }

    [Fact]
    public async Task Create_with_an_empty_image_counts_it_as_unreadable()
    {
        var empty = new JournalImageSource("vacia.png", () => Task.FromResult<Stream>(new MemoryStream()));

        var result = await Store.CreateAsync(new JournalCreate(Lyra, "Ankheg", null, null, [empty]));

        (result.Status, result.Unreadable).Should().Be((JournalStatus.NoImages, 1));
    }

    [Fact]
    public async Task Create_puts_the_note_of_a_pc_that_is_not_in_its_own_folder_under_its_own_diary()
    {
        var result = await Store.CreateAsync(new JournalCreate("Personajes/Torvald.md", "Ankheg", null, null, [Image("a.png")]));

        result.EntryPath.Should().Be("Personajes/Torvald/Diario/2026-11-16 Ankheg.md");
    }

    [Fact]
    public async Task Create_indexes_the_note_right_away()
    {
        var result = await CreateAsync("Ankheg", "a.png");

        Host.Current!.Index.Get(result.EntryPath!).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_removes_the_images_it_wrote_when_the_note_cannot_be_written()
    {
        Factory.Storage!.FailNoteWrites = true;

        var result = await CreateAsync("Ankheg", "a.png", "b.png");

        (result.Status, Names(Diary)).Should().Be((JournalStatus.Failed, string.Empty));
    }

    [Fact]
    public async Task Create_without_an_open_vault_does_nothing()
    {
        await Host.ForgetAsync();

        var result = await CreateAsync("Ankheg", "a.png");

        result.Status.Should().Be(JournalStatus.NoVault);
    }

    [Fact]
    public async Task Create_never_touches_a_note_that_was_already_there()
    {
        Directory.CreateDirectory(PathOf(Diary));
        File.WriteAllText(PathOf(Entry), "mía");

        var result = await CreateAsync("Ankheg", "a.png");

        (File.ReadAllText(PathOf(Entry)), result.EntryPath).Should().Be(("mía", Diary + "/2026-11-16 Ankheg 2.md"));
    }

    private static JournalImageSource Broken(string fileName) =>
        new(fileName, () => throw new ArgumentException("El IBrowserFile ya no es válido."));

    [Fact]
    public async Task Create_counts_an_image_whose_source_fails_to_open_as_unreadable_and_saves_the_rest()
    {
        var result = await Store.CreateAsync(new JournalCreate(Lyra, "Ankheg", null, null, [Broken("a.png"), Image("b.png")]));

        (result.Status, result.Unreadable, Names(Diary)).Should().Be((JournalStatus.Done, 1, "2026-11-16 Ankheg-1.png|2026-11-16 Ankheg.md"));
    }

    [Fact]
    public async Task Create_with_only_unreadable_images_is_NoImages_and_leaves_nothing()
    {
        var result = await Store.CreateAsync(new JournalCreate(Lyra, "Ankheg", null, null, [Broken("a.png")]));

        (result.Status, result.Unreadable, Names(Diary)).Should().Be((JournalStatus.NoImages, 1, string.Empty));
    }
}
