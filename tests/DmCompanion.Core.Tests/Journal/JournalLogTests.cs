using DmCompanion.Core.Journal;

namespace DmCompanion.Core.Tests.Journal;

public class JournalLogTests
{
    private static JournalEntry Entry(string path, string title, DateTimeOffset added, string[] images, string note = "", string owner = "Lyra") =>
        new(path, title, owner, "Personajes/Lyra/Diario", added, [.. images.Select(n => new JournalImage(n, "Personajes/Lyra/Diario/" + n, true, true))],
            null, note, []);

    private static readonly DateTimeOffset T1 = new(2026, 11, 16, 21, 34, 0, TimeSpan.FromHours(1));

    [Fact]
    public void Order_is_by_added_then_by_path()
    {
        var late = Entry("P/Diario/c.md", "C", T1.AddHours(1), []);
        var tieB = Entry("P/Diario/b.md", "B", T1, []);
        var tieA = Entry("P/Diario/a.md", "A", T1, []);

        JournalLog.Order([late, tieB, tieA]).Select(e => e.Title).Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Order_compares_instants_not_clock_readings()
    {
        var utcLater = Entry("P/Diario/a.md", "A", new DateTimeOffset(2026, 11, 16, 20, 30, 0, TimeSpan.Zero), []);
        var plusOneEarlier = Entry("P/Diario/b.md", "B", new DateTimeOffset(2026, 11, 16, 21, 0, 0, TimeSpan.FromHours(1)), []);

        JournalLog.Order([utcLater, plusOneEarlier]).Select(e => e.Title).Should().Equal("B", "A");
    }

    [Fact]
    public void Render_has_the_generated_frontmatter_and_the_intro_line() =>
        JournalLog.Render([], new DateTimeOffset(2026, 11, 20, 18, 5, 0, TimeSpan.Zero)).Should().Be(
            "---\ntype: journal-log\ngenerated: true\n---\nGenerado por DM Companion el 20/11/2026 18:05. Se sobrescribe al regenerar\n");

    [Fact]
    public void Render_writes_a_section_per_entry_with_heading_images_and_note()
    {
        var entry = Entry("P/Diario/a.md", "Ankheg", T1, ["a-1.jpg", "a-2.jpg"], "Salió del suelo.");

        JournalLog.Render([entry], T1).Should().Contain(
            "\n## Lyra · Ankheg · 16/11/2026\n\n![[Personajes/Lyra/Diario/a-1.jpg]]\n![[Personajes/Lyra/Diario/a-2.jpg]]\n\nSalió del suelo.\n");
    }

    [Fact]
    public void Render_skips_images_that_do_not_exist()
    {
        var entry = Entry("P/Diario/a.md", "A", T1, ["a-1.jpg"]) with { Images = [new JournalImage("a-1.jpg", "P/Diario/a-1.jpg", false, true)] };

        JournalLog.Render([entry], T1).Should().NotContain("![[");
    }

    [Theory]
    [InlineData(null, JournalLogDecision.Create)]
    [InlineData("---\ntype: journal-log\ngenerated: true\n---\nx", JournalLogDecision.Overwrite)]
    [InlineData("---\ntype: journal-log\n---\nmía", JournalLogDecision.ForeignNote)]
    [InlineData("Sin frontmatter", JournalLogDecision.ForeignNote)]
    [InlineData("---\ngenerated: false\n---\n", JournalLogDecision.ForeignNote)]
    public void Decide_overwrites_only_what_the_app_generated(string? existing, JournalLogDecision expected) =>
        JournalLog.Decide(existing).Should().Be(expected);
}
