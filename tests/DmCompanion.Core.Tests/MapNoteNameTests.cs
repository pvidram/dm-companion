using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public sealed class MapNoteNameTests
{
    [Fact]
    public void Replaces_the_name_and_keeps_everything_else()
    {
        var text = "---\ntype: map\nname: Faro\nimage: Mapas/Faro.png\n---\nCuerpo con name: x\n";

        MapNoteName.WithName(text, "Torre").Should().Be("---\ntype: map\nname: \"Torre\"\nimage: Mapas/Faro.png\n---\nCuerpo con name: x\n");
    }

    [Fact]
    public void Adds_the_name_when_the_frontmatter_has_none()
    {
        MapNoteName.WithName("---\ntype: map\n---\nHola\n", "Torre").Should().Be("---\nname: \"Torre\"\ntype: map\n---\nHola\n");
    }

    [Fact]
    public void Escapes_quotes_and_backslashes()
    {
        MapNoteName.WithName("---\nname: A\n---\n", "El \"Faro\" \\ 2").Should().Contain("name: \"El \\\"Faro\\\" \\\\ 2\"");
    }

    [Fact]
    public void Preserves_windows_line_endings()
    {
        MapNoteName.WithName("---\r\nname: A\r\n---\r\ntexto\r\n", "B").Should().Be("---\r\nname: \"B\"\r\n---\r\ntexto\r\n");
    }

    [Fact]
    public void Replaces_a_folded_multiline_name_entirely()
    {
        MapNoteName.WithName("---\nname: >\n  largo\n  nombre\ntype: map\n---\n", "B").Should().Be("---\nname: \"B\"\ntype: map\n---\n");
    }

    [Fact]
    public void Leaves_a_note_without_frontmatter_untouched()
    {
        MapNoteName.WithName("solo texto\n", "B").Should().Be("solo texto\n");
    }

    [Fact]
    public void Replaces_the_name_when_the_file_starts_with_a_byte_order_mark()
    {
        MapNoteName.WithName("\uFEFF---\nname: A\n---\ntexto\n", "B").Should().Be("\uFEFF---\nname: \"B\"\n---\ntexto\n");
    }

    [Fact]
    public void Adds_the_name_after_the_byte_order_mark_when_the_frontmatter_has_none()
    {
        MapNoteName.WithName("\uFEFF---\ntype: map\n---\n", "B").Should().Be("\uFEFF---\nname: \"B\"\ntype: map\n---\n");
    }
}
