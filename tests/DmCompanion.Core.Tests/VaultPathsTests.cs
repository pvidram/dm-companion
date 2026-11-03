using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class VaultPathsTests
{
    [Fact]
    public void Normalize_uses_forward_slashes_without_leading_slash() =>
        VaultPaths.Normalize(@"\Personajes\Varek.md").Should().Be("Personajes/Varek.md");

    [Theory]
    [InlineData(".obsidian/app.json", true)]
    [InlineData("Lugares/.trash/viejo.md", true)]
    [InlineData(".stfolder", true)]
    [InlineData("Lugares/Puerto Sal.md", false)]
    public void IsExcluded_skips_hidden_segments(string path, bool expected) =>
        VaultPaths.IsExcluded(path).Should().Be(expected);

    [Theory]
    [InlineData("Lugares/Puerto Sal.sync-conflict-20261103-101500-ABCDEF1.md", true)]
    [InlineData("Lugares/Puerto Sal.md", false)]
    public void IsConflictFile_detects_syncthing_conflicts(string path, bool expected) =>
        VaultPaths.IsConflictFile(path).Should().Be(expected);

    [Fact]
    public void GetConflictOriginal_strips_conflict_suffix() =>
        VaultPaths.GetConflictOriginal("Lugares/Puerto Sal.sync-conflict-20261103-101500-ABCDEF1.md")
            .Should().Be("Lugares/Puerto Sal.md");

    [Theory]
    [InlineData("Personajes/Varek.md", true)]
    [InlineData("Personajes/varek.MD", true)]
    [InlineData("_assets/varek.png", false)]
    [InlineData(".obsidian/plantilla.md", false)]
    [InlineData("Varek.sync-conflict-20261103-101500-ABCDEF1.md", false)]
    public void IsIndexable_accepts_only_visible_markdown_that_is_not_a_conflict(string path, bool expected) =>
        VaultPaths.IsIndexable(path).Should().Be(expected);

    [Theory]
    [InlineData("Personajes/Varek.md", "Personajes/Varek")]
    [InlineData("Varek", "Varek")]
    [InlineData(@"Personajes\Varek.MD", "Personajes/Varek")]
    public void LinkKey_drops_markdown_extension(string path, string expected) =>
        VaultPaths.LinkKey(path).Should().Be(expected);

    [Fact]
    public void FileTitle_is_file_name_without_extension() =>
        VaultPaths.FileTitle("Historia/Sesión 1.md").Should().Be("Sesión 1");
}
