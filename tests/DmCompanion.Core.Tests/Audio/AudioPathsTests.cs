using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public sealed class AudioPathsTests
{
    [Theory]
    [InlineData("_audio/a.mp3", true)]
    [InlineData("_audio/Música/b.OGG", true)]
    [InlineData("_audio/_dm/scenes/x.json", false)]
    [InlineData("_audio/_x/c.mp3", false)]
    [InlineData("notas/d.mp3", false)]
    [InlineData("_audio/e.sync-conflict-20261115-101010-ABCDEFG.mp3", false)]
    [InlineData("_audio/f.txt", false)]
    [InlineData("_audio/.oculta/g.mp3", false)]
    public void IsInLibrary_follows_the_library_rules(string path, bool expected) =>
        AudioPaths.IsInLibrary(path).Should().Be(expected);

    [Theory]
    [InlineData("a.mp3", "audio/mpeg")]
    [InlineData("a.OGG", "audio/ogg")]
    [InlineData("a.wav", "audio/wav")]
    [InlineData("a.m4a", "audio/mp4")]
    [InlineData("a.flac", "audio/flac")]
    [InlineData("a.txt", "application/octet-stream")]
    public void ContentTypeOf_maps_extensions(string path, string expected) =>
        AudioPaths.ContentTypeOf(path).Should().Be(expected);

    [Fact]
    public void Library_and_vault_paths_roundtrip() =>
        AudioPaths.ToVaultPath(AudioPaths.ToLibraryPath("_audio/Taberna/a.mp3")).Should().Be("_audio/Taberna/a.mp3");
}
