using DmCompanion.Core.Journal;

namespace DmCompanion.Core.Tests.Journal;

public class JournalSlidesTests
{
    private const string Folder = "Personajes/Lyra/Diario";
    private static readonly DateTimeOffset T1 = new(2026, 11, 10, 20, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset T2 = new(2026, 11, 16, 21, 34, 0, TimeSpan.FromHours(1));

    private static JournalImage Img(string name, bool exists = true, bool displayable = true) => new(name, $"{Folder}/{name}", exists, displayable);

    private static JournalEntry Entry(string title, DateTimeOffset added, string note, params JournalImage[] images) =>
        new($"{Folder}/{title}.md", title, "Lyra", Folder, added, images, null, note, []);

    [Fact]
    public void An_entry_has_one_slide_per_image_with_the_footer_of_the_spec() =>
        string.Join("|", JournalSlides.ForEntry(Entry("Ankheg", T2, "", Img("a.png"), Img("b.jpg"))).Select(s => s.Caption))
            .Should().Be("Lyra · Ankheg · imagen 1 de 2|Lyra · Ankheg · imagen 2 de 2");

    [Fact]
    public void An_entry_slide_points_to_its_image_in_the_diary_folder() =>
        JournalSlides.ForEntry(Entry("Ankheg", T2, "", Img("a.png")))[0].ImagePath.Should().Be($"{Folder}/a.png");

    [Fact]
    public void Missing_and_not_displayable_images_are_skipped_and_not_counted()
    {
        var slides = JournalSlides.ForEntry(Entry("Ankheg", T2, "", Img("a.heic", displayable: false), Img("falta.png", exists: false), Img("b.png")));

        (slides.Count, slides[0].Caption, slides[0].ImagePath).Should().Be((1, "Lyra · Ankheg · imagen 1 de 1", $"{Folder}/b.png"));
    }

    [Fact]
    public void A_note_adds_a_last_page_with_the_text_and_no_image()
    {
        var slides = JournalSlides.ForEntry(Entry("Ankheg", T2, "Salió del suelo.", Img("a.png")));

        (slides.Count, slides[^1].ImagePath, slides[^1].Text, slides[^1].Caption).Should().Be((2, null, "Salió del suelo.", "Lyra · Ankheg"));
    }

    [Fact]
    public void An_entry_with_a_note_and_no_displayable_image_is_only_the_note()
    {
        var slides = JournalSlides.ForEntry(Entry("Ankheg", T2, "Solo texto.", Img("a.heic", displayable: false)));

        (slides.Count, slides[0].Text).Should().Be((1, "Solo texto."));
    }

    [Fact]
    public void An_entry_with_nothing_to_show_has_no_slides() =>
        JournalSlides.ForEntry(Entry("Ankheg", T2, "", Img("falta.png", exists: false))).Should().BeEmpty();

    [Fact]
    public void The_log_goes_from_the_oldest_to_the_newest_entry_with_images_only() =>
        string.Join("|", JournalSlides.ForLog([Entry("Nueva", T2, "Con nota", Img("n.png")), Entry("Vieja", T1, "", Img("v1.png"), Img("v2.png"))]).Select(s => s.Caption))
            .Should().Be("Lyra · Vieja · imagen 1 de 2|Lyra · Vieja · imagen 2 de 2|Lyra · Nueva · imagen 1 de 1");

    [Fact]
    public void The_log_never_has_note_pages() =>
        JournalSlides.ForLog([Entry("Nueva", T2, "Con nota", Img("n.png"))]).All(s => s.Text is null).Should().BeTrue();

    [Fact]
    public void The_log_numbers_entries_among_those_that_have_images()
    {
        var slides = JournalSlides.ForLog([
            Entry("Vacia", T1, "", Img("falta.png", exists: false)),
            Entry("Dos", T2, "", Img("a.png"), Img("b.png")),
            Entry("Una", T2.AddMinutes(1), "", Img("c.png"))]);

        string.Join("|", slides.Select(s => $"{s.EntryNumber}/{s.EntryCount}")).Should().Be("1/2|1/2|2/2");
    }

    [Fact]
    public void The_log_of_nothing_is_empty() => JournalSlides.ForLog([]).Should().BeEmpty();

    [Fact]
    public void The_bar_label_of_the_log_is_the_entry_number() =>
        JournalSlides.ForLog([Entry("Una", T1, "", Img("a.png"))])[0].BarLabel(isLog: true).Should().Be("Entrada 1 de 1");

    [Fact]
    public void The_bar_label_of_an_entry_is_the_image_or_the_note()
    {
        var slides = JournalSlides.ForEntry(Entry("Ankheg", T2, "Nota", Img("a.png"), Img("b.png")));

        string.Join("|", slides.Select(s => s.BarLabel(isLog: false))).Should().Be("Imagen 1 de 2|Imagen 2 de 2|Nota");
    }

    [Fact]
    public void The_note_page_for_the_tv_has_the_secret_blocks_removed()
    {
        var slides = JournalSlides.ForEntry(Entry("Ankheg", T2, "Visible.\n%%no se ve%%\nOtra.", Img("a.png")));

        slides[^1].Text.Should().NotContain("no se ve").And.Contain("Visible.");
    }

    [Fact]
    public void A_note_that_is_only_secret_adds_no_page()
    {
        var slides = JournalSlides.ForEntry(Entry("Ankheg", T2, "%%todo secreto%%", Img("a.png")));

        slides.Select(s => s.Text).Should().Equal([null]);
    }

    [Fact]
    public void The_note_of_a_secret_entry_never_goes_to_the_tv_but_its_images_do()
    {
        var entry = Entry("Ankheg", T2, "Nota visible.", Img("a.png")) with { IsSecret = true };

        JournalSlides.ForEntry(entry).Select(s => (s.ImagePath, s.Text)).Should().Equal([($"{Folder}/a.png", (string?)null)]);
    }
}
