using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class MapCatalogTests
{
    private static VaultEntity Map(string path, string frontmatter) =>
        VaultEntityFactory.Create(path, $"---\ntype: map\n{frontmatter}\n---\n", DateTime.UnixEpoch);

    private static MapCatalog Build(IEnumerable<VaultEntity> maps, params string[] images) =>
        MapCatalog.Build(VaultIndex.Build(maps), images);

    [Fact]
    public void Build_orders_maps_by_name()
    {
        var catalog = Build([Map("Mapas/b.md", "name: Zeta"), Map("Mapas/a.md", "name: Alfa")]);

        catalog.Maps.Select(m => m.Name).Should().Equal("Alfa", "Zeta");
    }

    [Fact]
    public void Build_reports_unreferenced_image_in_maps_folder_as_orphan()
    {
        var catalog = Build([Map("Mapas/a.md", "image: Mapas/a.png")], "Mapas/a.png", "Mapas/sobra.png");

        catalog.OrphanImages.Should().Equal("Mapas/sobra.png");
    }

    [Fact]
    public void Build_compares_image_paths_normalized_and_ignoring_case()
    {
        var catalog = Build([Map("Mapas/a.md", "image: Mapas/A.PNG")], "mapas\\a.png");

        catalog.OrphanImages.Should().BeEmpty();
    }

    [Fact]
    public void Build_never_reports_images_outside_maps_folder()
    {
        var catalog = Build([], "_assets/lyra.png");

        catalog.OrphanImages.Should().BeEmpty();
    }

    [Fact]
    public void Build_never_reports_sync_conflict_images()
    {
        var catalog = Build([], "Mapas/a.sync-conflict-20260101-120000-ABCDEFG.png");

        catalog.OrphanImages.Should().BeEmpty();
    }
}
