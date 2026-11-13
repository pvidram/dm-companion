using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class MapDefinitionTests
{
    private static MapDefinition? From(string frontmatter, string path = "Mapas/Mazmorra.md") =>
        MapDefinition.FromEntity(VaultEntityFactory.Create(path, $"---\n{frontmatter}\n---\n", DateTime.UnixEpoch));

    [Fact]
    public void FromEntity_with_image_gives_image_path_and_zero_size()
    {
        var map = From("type: map\nname: Mazmorra\nimage: Mapas/Mazmorra del Kraken.png");

        map.Should().NotBeNull();
        map!.Should().Match<MapDefinition>(m =>
            m.NotePath == "Mapas/Mazmorra.md" && m.Name == "Mazmorra" && m.ImagePath == "Mapas/Mazmorra del Kraken.png"
            && m.Width == 0 && m.Height == 0);
    }

    [Fact]
    public void FromEntity_without_image_uses_canvas_size()
    {
        var map = From("type: map\ncanvas: { width: 1400, height: 700 }");

        map!.Should().Match<MapDefinition>(m => m.ImagePath == null && m.Width == 1400 && m.Height == 700);
    }

    [Fact]
    public void FromEntity_without_image_or_canvas_uses_defaults()
    {
        var map = From("type: map");

        map!.Should().Match<MapDefinition>(m => m.Width == 2800 && m.Height == 2100);
    }

    [Fact]
    public void FromEntity_falls_back_to_default_for_invalid_canvas_values()
    {
        var map = From("type: map\ncanvas: { width: ancho, height: -5 }");

        map!.Should().Match<MapDefinition>(m => m.Width == 2800 && m.Height == 2100);
    }

    [Fact]
    public void FromEntity_returns_null_for_other_types() =>
        From("type: note").Should().BeNull();

    [Fact]
    public void StatePath_is_next_to_the_note() =>
        From("type: map")!.StatePath.Should().Be("Mapas/Mazmorra.state.json");
}
