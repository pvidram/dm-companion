using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public sealed class AmbienceResolverTests
{
    private static readonly AudioLibrary Library = AudioLibrary.Build(
    [
        "_audio/Música/Tabernas/El Pony.mp3",
        "_audio/Música/Tabernas/Brindis.ogg",
        "_audio/Taberna/fuego.mp3",
        "_audio/Pony.mp3",
        "notas/ajeno.mp3",
    ]);

    private static AmbienceTarget Resolve(string? value, params string[] scenes) => AmbienceResolver.Resolve(value, scenes, Library);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_is_none(string? value) => Resolve(value).Kind.Should().Be(AmbienceKind.None);

    [Fact]
    public void Scene_resolves_by_name_ignoring_case_and_accents() =>
        Resolve("lúgubre", "Lugubre").Should().Be(new AmbienceTarget(AmbienceKind.Scene, "Lugubre", "Lugubre"));

    [Fact]
    public void Scene_name_is_matched_after_the_same_sanitizing_as_file_names() =>
        Resolve("Taberna: noche", "Taberna noche").Should().Be(new AmbienceTarget(AmbienceKind.Scene, "Taberna noche", "Taberna noche"));

    [Fact]
    public void Scene_wins_over_section_with_same_name() =>
        Resolve("Taberna", "Taberna").Kind.Should().Be(AmbienceKind.Scene);

    [Fact]
    public void Section_and_track_are_lax()
    {
        var results = new[] { Resolve("musica/tabernas"), Resolve("MUSICA/Tabernas/el pony.MP3") };

        results.Should().Equal(
            new AmbienceTarget(AmbienceKind.Section, "Tabernas", "Música/Tabernas"),
            new AmbienceTarget(AmbienceKind.Track, "El Pony", "Música/Tabernas/El Pony.mp3"));
    }

    [Fact]
    public void Wikilink_resolves_by_file_name_shortest_path() =>
        Resolve("[[pony.mp3]]").Reference.Should().Be("Pony.mp3");

    [Fact]
    public void Wikilink_without_extension_and_with_alias_resolves() =>
        Resolve("[[Brindis|el brindis]]").Reference.Should().Be("Música/Tabernas/Brindis.ogg");

    [Fact]
    public void Wikilink_outside_audio_is_not_found() =>
        Resolve("[[ajeno.mp3]]").Kind.Should().Be(AmbienceKind.NotFound);

    [Fact]
    public void Unknown_is_not_found_with_label() =>
        Resolve("Nada de nada").Should().Be(new AmbienceTarget(AmbienceKind.NotFound, "Nada de nada", null));
}
