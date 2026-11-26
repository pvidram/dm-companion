using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public sealed class NoteFileNameTests
{
    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("nul", "nul_")]
    [InlineData("Com1", "Com1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("aux.txt", "aux_.txt")]
    public void FromTitle_suffixes_windows_reserved_names(string title, string expected) =>
        NoteFileName.FromTitle(title).Should().Be(expected);

    [Theory]
    [InlineData("Consejo")]
    [InlineData("COM10")]
    [InlineData("Nulo")]
    public void FromTitle_keeps_names_that_only_look_reserved(string title) =>
        NoteFileName.FromTitle(title).Should().Be(title);

    [Fact]
    public void FromTitle_truncates_to_the_maximum_length() =>
        NoteFileName.FromTitle(new string('a', 200)).Length.Should().Be(NoteFileName.MaxLength);

    [Fact]
    public void FromTitle_does_not_end_in_dot_or_space_after_truncating() =>
        NoteFileName.FromTitle(new string('a', 119) + ". b").Should().Be(new string('a', 119));

    [Theory]
    [InlineData("???")] [InlineData("///")] [InlineData("  ")]
    public void TryFromTitle_returns_false_when_nothing_is_left(string title) =>
        NoteFileName.TryFromTitle(title, out _).Should().BeFalse();

    [Fact]
    public void TryFromTitle_returns_the_same_name_as_FromTitle()
    {
        NoteFileName.TryFromTitle("La Marca del Este", out var name);
        name.Should().Be(NoteFileName.FromTitle("La Marca del Este"));
    }
}
