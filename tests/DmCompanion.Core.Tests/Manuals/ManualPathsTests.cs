using DmCompanion.Core.Manuals;

namespace DmCompanion.Core.Tests.Manuals;

public class ManualPathsTests
{
    [Fact]
    public void Pdf_under_system_folder_is_a_manual() =>
        ManualPaths.IsManualOf("manuales/dnd5e/phb.pdf", "dnd5e").Should().BeTrue();

    [Fact]
    public void Subfolders_count() =>
        ManualPaths.IsManualOf("manuales/dnd5e/Core/phb.pdf", "dnd5e").Should().BeTrue();

    [Fact]
    public void Other_system_is_not() =>
        ManualPaths.IsManualOf("manuales/cosmere/phb.pdf", "dnd5e").Should().BeFalse();

    [Fact]
    public void Case_insensitive() =>
        ManualPaths.IsManualOf("Manuales/DND5E/PHB.PDF", "dnd5e").Should().BeTrue();

    [Theory]
    [InlineData("manuales/dnd5e/phb.sync-conflict-20261116-101500-ABCDEF1.pdf")]
    [InlineData("manuales/dnd5e/Core/phb.sync-conflict-20261116-101500-ABCDEF1.pdf")]
    public void Sync_conflict_copy_is_not(string path) =>
        ManualPaths.IsManualOf(path, "dnd5e").Should().BeFalse();

    [Fact]
    public void Non_pdf_is_not() =>
        ManualPaths.IsManualOf("manuales/dnd5e/phb.md", "dnd5e").Should().BeFalse();

    [Theory]
    [InlineData("manuales/dnd5e/phb.pdf", "")]
    [InlineData("manuales/dnd5e/Core/phb.pdf", "Core")]
    [InlineData("manuales/dnd5e/Core/Extra/phb.pdf", "Core/Extra")]
    public void Group_is_the_subfolder(string path, string expected) =>
        ManualPaths.GroupOf(path, "dnd5e").Should().Be(expected);

    [Fact]
    public void Bookmarks_path_replaces_extension() =>
        ManualPaths.BookmarksPathOf("x/y.pdf").Should().Be("x/y.marcadores.json");

    [Fact]
    public void Position_key_is_normalized() =>
        ManualPaths.PositionKeyOf(@"\manuales\dnd5e\phb.pdf").Should().Be("manual.page.manuales/dnd5e/phb.pdf");
}
