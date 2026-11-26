using DmCompanion.Core.Journal;

namespace DmCompanion.Core.Tests.Journal;

public class JournalReaderTests
{
    private const string Path = "Personajes/Lyra/Diario/2026-11-16 Ankheg.md";

    private const string Valid =
        "---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\nimages:\n  - \"2026-11-16 Ankheg-1.jpg\"\n  - \"2026-11-16 Ankheg-2.jpg\"\ncreature: \"srd:criaturas/ankheg\"\n---\nSalió del suelo.\n";

    private static JournalEntry Read(string content, Func<string, bool>? exists = null) =>
        JournalReader.Read(Path, content, new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc), exists ?? (_ => true));

    [Fact]
    public void Valid_entry_has_title_owner_and_date() =>
        Read(Valid).Should().Match<JournalEntry>(e =>
            e.Title == "Ankheg" && e.OwnerName == "Lyra" && e.DiaryFolder == "Personajes/Lyra/Diario"
            && e.Added == new DateTimeOffset(2026, 11, 16, 21, 34, 0, TimeSpan.FromHours(1)));

    [Fact]
    public void Valid_entry_lists_images_in_order_with_their_paths() =>
        Read(Valid).Images.Select(i => i.Path).Should().Equal(
            "Personajes/Lyra/Diario/2026-11-16 Ankheg-1.jpg", "Personajes/Lyra/Diario/2026-11-16 Ankheg-2.jpg");

    [Fact]
    public void Valid_entry_has_creature_note_and_no_warnings() =>
        Read(Valid).Should().Match<JournalEntry>(e =>
            e.Creature == "srd:criaturas/ankheg" && e.Note == "Salió del suelo." && e.Warnings.Count == 0);

    [Fact]
    public void Missing_image_is_flagged_as_not_existing() =>
        Read(Valid, path => path.EndsWith("-1.jpg", StringComparison.Ordinal)).Images.Select(i => i.Exists).Should().Equal(true, false);

    [Fact]
    public void Missing_image_adds_a_warning() =>
        Read(Valid, path => path.EndsWith("-1.jpg", StringComparison.Ordinal)).Warnings
            .Should().Equal("Imagen no encontrada: 2026-11-16 Ankheg-2.jpg");

    [Fact]
    public void Invalid_frontmatter_gives_one_warning_and_keeps_the_body()
    {
        var entry = Read("---\ntype: journal\nimages: [sin cerrar\n---\nHola");

        (entry.Warnings.Count, entry.Warnings[0].StartsWith("Frontmatter YAML inválido", StringComparison.Ordinal), entry.Note)
            .Should().Be((1, true, "Hola"));
    }

    [Fact]
    public void Missing_added_falls_back_to_the_file_date_with_a_warning()
    {
        var entry = Read("---\ntype: journal\nimages:\n  - \"a.jpg\"\n---\n");

        (entry.Added.UtcDateTime, entry.Warnings.Count).Should().Be((new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc), 1));
    }

    [Fact]
    public void Image_name_with_a_path_is_rejected_with_a_warning() =>
        Read("---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\nimages:\n  - \"../x.jpg\"\n---\n").Warnings
            .Should().Equal("Nombre de imagen no válido: ../x.jpg");

    [Fact]
    public void Heic_exists_but_is_not_displayable()
    {
        var entry = Read("---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\nimages:\n  - \"a.heic\"\n---\n");

        entry.Images[0].Should().Match<JournalImage>(i => i.Exists && !i.Displayable);
    }

    [Fact]
    public void Unquoted_wikilink_creature_is_read_back_as_a_wikilink() =>
        Read("---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\ncreature: [[Ankheg]]\n---\n").Creature.Should().Be("[[Ankheg]]");

    [Fact]
    public void Is_journal_matches_the_type_ignoring_case()
    {
        var fields = new Dictionary<string, object?> { ["type"] = "Journal" };

        JournalReader.IsJournal(fields).Should().BeTrue();
    }

    [Fact]
    public void An_entry_with_secret_true_is_secret_and_a_normal_one_is_not() =>
        (Read(Valid.Replace("type: journal", "type: journal\nsecret: true", StringComparison.Ordinal)).IsSecret, Read(Valid).IsSecret).Should().Be((true, false));

    [Fact]
    public void An_entry_with_broken_yaml_is_treated_as_secret() =>
        Read("---\ntype: [journal\n---\nHola\n").IsSecret.Should().BeTrue();
}
