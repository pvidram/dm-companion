using DmCompanion.Core.Audio;

namespace DmCompanion.Screens.Tests;

// Contrato de IAudioPlayer: un canal que nace durante la pausa global no suena hasta reanudar.
public sealed class AudioMixerPauseContractTests : AudioMixerTestBase
{
    [Fact]
    public async Task Switching_a_waiting_effect_to_loop_while_paused_stays_silent_until_resume()
    {
        var id = await Mixer.PlayEffectAsync(Cuervo, EffectRepeat.WithPause(5, 5));
        Player.RaiseEnded(ChannelIdOf(Cuervo));
        await Mixer.PauseAllAsync();

        await Mixer.SetEffectRepeatAsync(id!, EffectRepeat.Loop);
        var pausedBeforeResume = ChannelOf(Cuervo)!.Paused;
        await Mixer.ResumeAllAsync();

        (pausedBeforeResume, ChannelOf(Cuervo)!.Paused).Should().Be((true, false));
    }

    [Fact]
    public async Task Next_playlist_track_after_a_late_ended_stays_paused()
    {
        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"));
        await Mixer.PauseAllAsync();

        EndBackgroundTrack();

        ChannelOf(Mixer.State.Background!.CurrentTrack!)!.Paused.Should().BeTrue();
    }
}
