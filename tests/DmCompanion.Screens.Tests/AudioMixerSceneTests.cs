using DmCompanion.Core.Audio;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class AudioMixerSceneTests : AudioMixerTestBase
{
    [Fact]
    public async Task Launch_scene_returns_missing_parts()
    {
        Player.MissingPaths.Add(AudioPaths.ToVaultPath(Cuervo));
        var scene = new AudioScene("Cripta", new SceneBackground(new TrackBackground("no-existe.mp3"), 0.5),
            [new SceneEffect(Lluvia, 0.5, EffectRepeat.Loop), new SceneEffect(Cuervo, 0.5, EffectRepeat.WithPause(5, 10))], 1);

        var missing = await Mixer.LaunchSceneAsync(scene);

        (missing.ToList(), Mixer.State.Effects.Select(e => e.Track).ToList())
            .Should().BeEquivalentTo((new List<string> { "no-existe.mp3", Cuervo }, new List<string> { Lluvia }));
    }

    [Fact]
    public async Task Scene_launch_follows_the_transition_plan()
    {
        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"));
        await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);
        await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);
        await Mixer.PlayEffectAsync(Cuervo);
        Advance(2);
        var lluviaChannel = ChannelIdOf(Lluvia);
        var scene = new AudioScene("Tormenta", new SceneBackground(new TrackBackground(Fondo), 0.5),
            [new SceneEffect(Lluvia, 0.3, EffectRepeat.Loop), new SceneEffect(Cuervo, 0.4, EffectRepeat.WithPause(5, 5))], 0.5);

        await Mixer.LaunchSceneAsync(scene);
        Advance(1);
        var masterHalfway = Math.Round(Mixer.State.MasterVolume, 3);
        Advance(1);

        var state = Mixer.State;
        new
        {
            Background = state.Background!.Source,
            Effects = state.Effects.Select(e => (e.Track, e.Repeat.Kind)).ToList(),
            KeptSameChannel = Player.Channels.ContainsKey(lluviaChannel),
            LluviaVolume = VolumeOf(Lluvia),
            Playing = PlayingPaths(),
            MasterHalfway = masterHalfway,
            state.MasterVolume,
            state.SceneName,
        }.Should().BeEquivalentTo(new
        {
            Background = (BackgroundSource)new TrackBackground(Fondo),
            Effects = new List<(string, RepeatKind)> { (Lluvia, RepeatKind.Loop), (Cuervo, RepeatKind.Once), (Cuervo, RepeatKind.RepeatWithPause) },
            KeptSameChannel = true,
            LluviaVolume = (double?)0.15,
            Playing = new[] { Cuervo, Cuervo, Lluvia, Fondo }.Order().ToList(),
            MasterHalfway = 0.75,
            MasterVolume = 0.5,
            SceneName = "Tormenta",
        });
    }

    [Fact]
    public async Task New_scene_during_a_crossfade_ends_in_the_last_scene_without_orphans()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Lluvia));
        Advance(0.5);
        await Mixer.LaunchSceneAsync(new AudioScene("Taberna", new SceneBackground(new SectionBackground("Taberna"), 0.8),
            [new SceneEffect(Trueno, 0.5, EffectRepeat.Loop)], 1));
        Advance(0.5);

        await Mixer.LaunchSceneAsync(new AudioScene("Bosque", new SceneBackground(new TrackBackground(Fondo), 0.8),
            [new SceneEffect(Cuervo, 0.5, EffectRepeat.Loop)], 0.6));
        Advance(3);

        (PlayingPaths(), Mixer.State.Background!.Source, Mixer.State.SceneName)
            .Should().BeEquivalentTo((new[] { Cuervo, Fondo }.Order().ToList(), (BackgroundSource)new TrackBackground(Fondo), "Bosque"));
    }

    [Fact]
    public async Task Scene_without_background_turns_the_background_off()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);

        await Mixer.LaunchSceneAsync(new AudioScene("Silencio", null, [new SceneEffect(Lluvia, 0.5, EffectRepeat.Loop)], 1));
        Advance(2);

        (Mixer.State.Background, PlayingPaths()).Should().BeEquivalentTo(((BackgroundState?)null, new[] { Lluvia }));
    }

    [Fact]
    public async Task Master_fade_notifies_a_few_times_and_ends_on_the_scene_volume()
    {
        var notices = 0;
        await Mixer.LaunchSceneAsync(new AudioScene("Calma", null, [], 0.5));
        Mixer.Changed += (_, _) => notices++;

        // El reloj falso dispara un temporizador periódico una vez por avance: se avanza en pasos de fundido.
        for (var i = 0; i < 60; i++) Advance(0.05);

        (notices is > 0 and <= 11, Mixer.State.MasterVolume).Should().Be((true, 0.5));
    }

    [Fact]
    public async Task Capture_scene_skips_once_effects()
    {
        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"), 0.6);
        await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);
        await Mixer.PlayEffectAsync(Cuervo, EffectRepeat.WithPause(5, 20));
        await Mixer.PlayEffectAsync(Trueno);
        await Mixer.SetMasterVolumeAsync(0.7);

        var scene = Mixer.CaptureScene("Taberna llena");

        scene.Should().BeEquivalentTo(new AudioScene("Taberna llena", new SceneBackground(new SectionBackground("Taberna"), 0.6),
            [new SceneEffect(Lluvia, AudioDefaults.DefaultVolume, EffectRepeat.Loop),
             new SceneEffect(Cuervo, AudioDefaults.DefaultVolume, EffectRepeat.WithPause(5, 20))], 0.7));
    }
}
