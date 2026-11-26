using DmCompanion.Core.Journal;

namespace DmCompanion.Core.Tests.Journal;

public class JournalNoteTests
{
    private static readonly DateTimeOffset Added = new(2026, 11, 16, 21, 34, 0, TimeSpan.FromHours(1));

    [Fact]
    public void Compose_writes_the_frontmatter_in_the_documented_shape() =>
        JournalNote.Compose(Added, ["a-1.jpg", "a-2.png"], "srd:criaturas/ankheg", "Nota.").Should().Be(
            "---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\nimages:\n  - \"a-1.jpg\"\n  - \"a-2.png\"\ncreature: \"srd:criaturas/ankheg\"\n---\nNota.\n");

    [Fact]
    public void Compose_omits_creature_and_note_when_empty() =>
        JournalNote.Compose(Added, ["a-1.jpg"], null, "  ").Should().Be(
            "---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\nimages:\n  - \"a-1.jpg\"\n---\n");

    [Fact]
    public void Compose_escapes_quotes_in_names() =>
        JournalNote.Compose(Added, ["a \"x\".jpg"], null, null).Should().Contain("  - \"a \\\"x\\\".jpg\"\n");

    [Fact]
    public void Compose_keeps_extra_fields_from_the_previous_note()
    {
        var previous = new Dictionary<string, object?> { ["type"] = "journal", ["mood"] = "tenso", ["images"] = new List<object?> { "viejo.jpg" } };

        JournalNote.Compose(Added, ["a-1.jpg"], null, null, previous).Should().Contain("mood: tenso").And.NotContain("viejo.jpg");
    }

    [Fact]
    public void Compose_then_read_is_a_round_trip()
    {
        var text = JournalNote.Compose(Added, ["x-1.jpg"], "[[Ankheg]]", "Línea 1\n\nLínea 2");

        var entry = JournalReader.Read("P/Diario/2026-11-16 X.md", text, DateTime.UtcNow, _ => true);

        (entry.Added, entry.Images[0].Name, entry.Creature, entry.Note).Should().Be((Added, "x-1.jpg", "[[Ankheg]]", "Línea 1\n\nLínea 2"));
    }
}
