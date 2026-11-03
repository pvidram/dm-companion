using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class VaultTreeTests
{
    private static VaultEntity Note(string path) => VaultEntityFactory.Create(path, "", DateTime.UnixEpoch);

    private static readonly VaultTreeNode Root = VaultTree.Build([
        Note("Lugares/Puerto Sal.md"),
        Note("Lugares/Costa/Faro Viejo.md"),
        Note("Índice.md"),
        Note("Abismo.md"),
    ]);

    [Fact]
    public void Build_lists_folders_before_notes() =>
        Root.Children.Select(c => c.Name).Should().Equal("Lugares", "Abismo", "Índice");

    [Fact]
    public void Build_nests_subfolders() =>
        Root.Children[0].Children.Select(c => c.Name).Should().Equal("Costa", "Puerto Sal");

    [Fact]
    public void Build_sets_folder_paths() =>
        Root.Children[0].Children[0].Path.Should().Be("Lugares/Costa");

    [Fact]
    public void Build_links_notes_to_entities() =>
        Root.Children[0].Children[1].Entity!.RelativePath.Should().Be("Lugares/Puerto Sal.md");

    [Fact]
    public void Folders_have_no_entity() =>
        Root.Children[0].IsFolder.Should().BeTrue();
}
