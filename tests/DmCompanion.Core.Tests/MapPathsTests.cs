using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class MapPathsTests
{
    [Theory]
    [InlineData("Mapas/X.md", "Mapas/X.state.json")]
    [InlineData("Mapas/Mazmorra del Kraken (ñandú).md", "Mapas/Mazmorra del Kraken (ñandú).state.json")]
    [InlineData(@"Mapas\Sub\X.md", "Mapas/Sub/X.state.json")]
    public void StatePathFor_keeps_the_base_name(string note, string expected) =>
        MapPaths.StatePathFor(note).Should().Be(expected);

    [Theory]
    [InlineData("a.PNG", true)]
    [InlineData("Mapas/b.jpeg", true)]
    [InlineData("c.webp", true)]
    [InlineData("a.svg", false)]
    [InlineData("a.md", false)]
    public void IsImage_uses_a_case_insensitive_whitelist(string path, bool expected) =>
        MapPaths.IsImage(path).Should().Be(expected);

    [Theory]
    [InlineData("Mapas/X.state.json", true)]
    [InlineData("Mapas/X.STATE.JSON", true)]
    [InlineData("Mapas/X.json", false)]
    public void IsStateFile_matches_suffix(string path, bool expected) =>
        MapPaths.IsStateFile(path).Should().Be(expected);

    [Fact]
    public void MapsFolder_is_Mapas() => MapPaths.MapsFolder.Should().Be("Mapas");
}
