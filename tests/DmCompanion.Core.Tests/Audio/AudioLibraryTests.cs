using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public sealed class AudioLibraryTests
{
    private static readonly string[] Files =
    [
        "_audio/Taberna/Fuego.mp3",
        "_audio/Bosque/Viento.ogg",
        "_audio/Bosque/Noche/Grillos.wav",
        "_audio/Fuego.mp3",
        "_audio/_dm/scenes/x.json",
        "_audio/_oculta/z.mp3",
        "notas/ajeno.mp3",
    ];

    private static readonly AudioLibrary Library = AudioLibrary.Build(Files);

    [Fact]
    public void Build_groups_tracks_by_section_sorted_by_path() =>
        Library.Sections.Select(section => section.Path).Should().Equal("", "Bosque", "Bosque/Noche", "Taberna");

    [Fact]
    public void Build_sorts_tracks_by_path() =>
        Library.Tracks.Select(track => track.Path).Should()
            .Equal("Bosque/Noche/Grillos.wav", "Bosque/Viento.ogg", "Fuego.mp3", "Taberna/Fuego.mp3");

    [Fact]
    public void Build_fills_name_and_section_of_a_track() =>
        Library.FindTrack("Bosque/Viento.ogg").Should().Be(new AudioTrack("Bosque/Viento.ogg", "Viento", "Bosque"));

    [Fact]
    public void Build_names_a_section_after_its_folder() =>
        Library.FindSection("Bosque/Noche")!.Name.Should().Be("Noche");

    [Fact]
    public void Search_ignores_case_and_accents() =>
        AudioLibrary.Build(["_audio/Taberna/Música.mp3"]).Search("tabérna").Should().ContainSingle();

    [Fact]
    public void Search_with_blank_query_returns_nothing() => Library.Search("  ").Should().BeEmpty();

    [Fact]
    public void FindByFileName_returns_shortest_path_first() =>
        Library.FindByFileName("fuego").Select(track => track.Path).Should().Equal("Fuego.mp3", "Taberna/Fuego.mp3");

    [Fact]
    public void FindByFileName_accepts_the_extension() =>
        Library.FindByFileName("VIENTO.ogg").Should().ContainSingle();

    [Fact]
    public void FindByFileName_with_other_extension_finds_nothing() =>
        Library.FindByFileName("Viento.mp3").Should().BeEmpty();

    [Fact]
    public void FindSection_is_lax() => Library.FindSection("bosque/NOCHE/").Should().NotBeNull();

    [Fact]
    public void FindTrack_is_lax() => Library.FindTrack("BOSQUE/viento.OGG").Should().NotBeNull();

    [Fact]
    public void Build_keeps_paths_that_only_differ_in_case() =>
        AudioLibrary.Build(["_audio/A.mp3", "_audio/a.mp3"]).Tracks.Should().HaveCount(2);

    [Fact]
    public void Empty_has_no_sections() => AudioLibrary.Empty.Sections.Should().BeEmpty();
}
