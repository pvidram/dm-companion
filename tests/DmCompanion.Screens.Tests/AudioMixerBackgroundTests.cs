using DmCompanion.Core.Audio;

namespace DmCompanion.Screens.Tests;

public sealed class AudioMixerBackgroundTests : AudioMixerTestBase
{
    [Fact]
    public async Task Background_crossfades_over_two_seconds()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);

        await Mixer.SetBackgroundAsync(new TrackBackground(Lluvia));
        var atStart = (VolumeOf(Lluvia), VolumeOf(Fondo));
        Advance(1);
        var atOne = (VolumeOf(Lluvia), VolumeOf(Fondo));
        Advance(1);
        var atTwo = (VolumeOf(Lluvia), VolumeOf(Fondo));

        (atStart, atOne, atTwo).Should().Be(((0.0, 0.8), (0.4, 0.4), (0.8, (double?)null)));
    }

    [Fact]
    public async Task Same_background_with_another_volume_keeps_the_scene_name()
    {
        await Mixer.LaunchSceneAsync(new AudioScene("Bosque", new SceneBackground(new TrackBackground(Fondo), 0.8), [], 1));

        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo), 0.5);

        Mixer.State.SceneName.Should().Be("Bosque");
    }

    [Fact]
    public async Task Another_background_clears_the_scene_name()
    {
        await Mixer.LaunchSceneAsync(new AudioScene("Bosque", new SceneBackground(new TrackBackground(Fondo), 0.8), [], 1));

        await Mixer.SetBackgroundAsync(new TrackBackground(Lluvia));

        Mixer.State.SceneName.Should().BeNull();
    }

    [Fact]
    public async Task Track_background_uses_native_loop()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));

        ChannelOf(Fondo)!.Loop.Should().BeTrue();
    }

    [Fact]
    public async Task Section_background_plays_shuffled_and_continues_on_ended()
    {
        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"));
        var played = new List<string> { Mixer.State.Background!.CurrentTrack! };
        var loops = ChannelOf(played[0])!.Loop;

        for (var i = 0; i < 2; i++)
        {
            EndBackgroundTrack();
            played.Add(Mixer.State.Background!.CurrentTrack!);
        }

        (loops, played.Order().ToList()).Should().BeEquivalentTo((false, Taberna.ToList()), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Next_section_track_during_the_fade_in_keeps_fading_in()
    {
        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"));
        Advance(0.5);

        EndBackgroundTrack();
        var next = Mixer.State.Background!.CurrentTrack!;
        var atStart = VolumeOf(next);
        Advance(1.5);

        (atStart, VolumeOf(next)).Should().Be((0.2, 0.8));
    }

    [Fact]
    public async Task Single_track_section_keeps_playing()
    {
        await Mixer.SetBackgroundAsync(new SectionBackground("Bosque"));

        ChannelOf(Lluvia)!.Loop.Should().BeTrue();
    }

    [Fact]
    public async Task Empty_section_is_not_set()
    {
        var set = await Mixer.SetBackgroundAsync(new SectionBackground("Vacia"));

        (set, Mixer.State.Background, Player.Channels.Count).Should().Be((false, null, 0));
    }

    [Fact]
    public async Task Changing_the_background_by_hand_clears_the_scene_name()
    {
        await Mixer.LaunchSceneAsync(new AudioScene("Taberna", new SceneBackground(new SectionBackground("Taberna"), 0.5), [], 1));

        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));

        Mixer.State.SceneName.Should().BeNull();
    }

    [Fact]
    public async Task Same_background_again_only_changes_the_volume()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);

        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo), 0.5);

        (Player.Channels.Count, VolumeOf(Fondo)).Should().Be((1, 0.5));
    }

    [Fact]
    public async Task Stopping_the_background_while_paused_does_not_sound_on_resume()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);
        await Mixer.PauseAllAsync();

        await Mixer.StopBackgroundAsync();
        await Mixer.ResumeAllAsync();

        ChannelOf(Fondo).Should().BeNull();
    }

    [Fact]
    public async Task Stop_background_fades_out_in_half_a_second()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);

        await Mixer.StopBackgroundAsync();
        Advance(0.5);

        (Mixer.State.Background, Player.Channels.Count).Should().Be((null, 0));
    }
}
