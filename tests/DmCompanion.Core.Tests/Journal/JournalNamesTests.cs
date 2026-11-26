using DmCompanion.Core.Journal;

namespace DmCompanion.Core.Tests.Journal;

public class JournalNamesTests
{
    private static readonly DateTimeOffset Added = new(2026, 11, 16, 21, 34, 0, TimeSpan.FromHours(1));

    [Theory]
    [InlineData(null, "Entrada")]
    [InlineData("", "Entrada")]
    [InlineData("   ", "Entrada")]
    [InlineData("Ankheg", "Ankheg")]
    [InlineData("a/b:c", "a-b-c")]
    [InlineData("¿Quién? #1 [x]", "¿Quién- -1 -x-")]
    [InlineData("  dos   espacios. ", "dos espacios")]
    [InlineData("CON", "CON_")]
    public void Safe_title_replaces_invalid_characters_with_a_dash(string? title, string expected) =>
        JournalNames.SafeTitle(title).Should().Be(expected);

    [Fact]
    public void Safe_title_is_capped() =>
        JournalNames.SafeTitle(new string('a', 300)).Length.Should().Be(100);

    [Fact]
    public void Base_name_is_the_date_and_the_title() =>
        JournalNames.BaseName(Added, "Ankheg").Should().Be("2026-11-16 Ankheg");

    [Fact]
    public void Base_name_without_title_uses_Entrada() =>
        JournalNames.BaseName(Added, null).Should().Be("2026-11-16 Entrada");

    [Fact]
    public void Unique_keeps_a_free_name() =>
        JournalNames.Unique("x", _ => false).Should().Be("x");

    [Fact]
    public void Unique_adds_2_then_3() =>
        JournalNames.Unique("x", name => name is "x" or "x 2").Should().Be("x 3");

    [Fact]
    public void Image_name_keeps_the_extension_in_lowercase() =>
        JournalNames.ImageName("2026-11-16 Ankheg", 1, ".JPG").Should().Be("2026-11-16 Ankheg-1.jpg");

    [Theory]
    [InlineData("2026-11-16 Ankheg", "Ankheg")]
    [InlineData("2026-11-16 Ankheg 2", "Ankheg 2")]
    [InlineData("Sin fecha", "Sin fecha")]
    [InlineData("2026-11-16 ", "Entrada")]
    public void Title_is_the_file_name_without_the_date(string baseName, string expected) =>
        JournalNames.TitleOf(baseName).Should().Be(expected);

    [Fact]
    public void Renamed_base_keeps_the_original_date() =>
        JournalNames.RenamedBase("2026-11-10 Ankheg", Added, "Dragón").Should().Be("2026-11-10 Dragón");

    [Fact]
    public void Renamed_base_without_date_uses_added() =>
        JournalNames.RenamedBase("Ankheg", Added, "Dragón").Should().Be("2026-11-16 Dragón");
}
