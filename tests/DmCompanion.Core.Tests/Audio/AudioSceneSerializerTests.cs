using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public sealed class AudioSceneSerializerTests
{
    [Fact]
    public void Round_trips_track_and_section_backgrounds()
    {
        var effects = new[] { new SceneEffect("Fx/lluvia.ogg", 0.4, EffectRepeat.Loop), new SceneEffect("Fx/trueno.ogg", 0.8, EffectRepeat.WithPause(5, 20)) };
        var scene = new AudioScene("Taberna", new SceneBackground(new SectionBackground("Música/Tabernas"), 0.6), effects, 0.9);

        var (read, repairs) = AudioSceneSerializer.Read(AudioSceneSerializer.Write(scene), "Taberna");

        read.Should().BeEquivalentTo(scene);
        repairs.Should().Be(0);
    }

    [Fact]
    public void Round_trips_a_track_background()
    {
        var scene = new AudioScene("X", new SceneBackground(new TrackBackground("a/b.mp3"), 1), [], 0.5);

        AudioSceneSerializer.Read(AudioSceneSerializer.Write(scene), "X").Scene!.Background!.Source.Should().Be(new TrackBackground("a/b.mp3"));
    }

    [Fact]
    public void Written_json_has_version_and_no_name() =>
        AudioSceneSerializer.Write(new AudioScene("Secreto", null, [], 1)).Should().Contain("\"version\": 1").And.NotContain("Secreto");

    [Fact]
    public void Clamps_volumes_and_counts_repairs()
    {
        const string json = """{"version":1,"masterVolume":3,"background":{"track":"a.mp3","volume":-1},"effects":[{"track":"b.mp3","volume":2,"repeat":{"kind":"pause","minPause":0,"maxPause":0}}]}""";

        var (scene, repairs) = AudioSceneSerializer.Read(json, "S");

        (scene!.MasterVolume, scene.Background!.Volume, scene.Effects[0].Volume, repairs).Should().Be((1.0, 0.0, 1.0, 4));
    }

    [Fact]
    public void Normalizes_the_pause_range()
    {
        const string json = """{"effects":[{"track":"b.mp3","volume":1,"repeat":{"kind":"pause","minPause":8,"maxPause":3}}]}""";

        AudioSceneSerializer.Read(json, "S").Scene!.Effects[0].Repeat.Should().Be(EffectRepeat.WithPause(8, 8));
    }

    [Fact]
    public void Drops_null_effects_and_effects_without_a_track()
    {
        const string json = """{"effects":[null,{"volume":1},{"track":"  "},{"track":"ok.mp3"}]}""";

        AudioSceneSerializer.Read(json, "S").Scene!.Effects.Select(effect => effect.Track).Should().Equal("ok.mp3");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{no es json")]
    [InlineData("[1,2]")]
    public void Invalid_json_returns_null(string json) =>
        AudioSceneSerializer.Read(json, "S").Should().Be((null, 0));
}
