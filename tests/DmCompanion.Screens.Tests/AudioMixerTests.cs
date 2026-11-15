using DmCompanion.Core.Audio;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Tests;

public sealed class AudioMixerTests : AudioMixerTestBase
{
    // ---- Efectos ----

    [Fact]
    public async Task Effect_plays_once_by_default_and_leaves_on_ended()
    {
        await Mixer.PlayEffectAsync(Trueno);
        var loops = ChannelOf(Trueno)!.Loop;

        Player.RaiseEnded(ChannelIdOf(Trueno));

        (loops, Mixer.State.Effects.Count).Should().Be((false, 0));
    }

    [Fact]
    public async Task Loop_effect_uses_native_loop()
    {
        await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);

        ChannelOf(Trueno)!.Loop.Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_effect_is_not_played()
    {
        var id = await Mixer.PlayEffectAsync("Efectos/no-existe.wav");

        (id, Player.Channels.Count).Should().Be((null, 0));
    }

    [Fact]
    public async Task Repeat_with_pause_waits_then_plays_again()
    {
        await Mixer.PlayEffectAsync(Cuervo, EffectRepeat.WithPause(5, 5));
        Player.RaiseEnded(ChannelIdOf(Cuervo));
        var waiting = Mixer.State.Effects.Single().Waiting;

        Advance(4.9);
        var playingBeforePause = ChannelOf(Cuervo) is not null;
        Advance(0.1);

        (waiting, playingBeforePause, ChannelOf(Cuervo) is not null).Should().Be((true, false, true));
    }

    [Fact]
    public async Task Stopping_during_the_pause_never_plays_again()
    {
        var id = await Mixer.PlayEffectAsync(Cuervo, EffectRepeat.WithPause(5, 5));
        Player.RaiseEnded(ChannelIdOf(Cuervo));

        await Mixer.StopEffectAsync(id!);
        Advance(60);

        (Player.Channels.Count, Mixer.State.Effects.Count).Should().Be((0, 0));
    }

    [Fact]
    public async Task Pause_all_suspends_the_wait_and_resume_continues_it()
    {
        await Mixer.PlayEffectAsync(Cuervo, EffectRepeat.WithPause(10, 10));
        Player.RaiseEnded(ChannelIdOf(Cuervo));
        Advance(4);

        await Mixer.PauseAllAsync();
        Advance(30);
        var playedWhilePaused = ChannelOf(Cuervo) is not null;
        await Mixer.ResumeAllAsync();
        Advance(5.9);
        var playedEarly = ChannelOf(Cuervo) is not null;
        Advance(0.1);

        (playedWhilePaused, playedEarly, ChannelOf(Cuervo) is not null).Should().Be((false, false, true));
    }

    [Fact]
    public async Task Stopping_an_effect_while_paused_does_not_sound_on_resume()
    {
        var id = await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);
        await Mixer.PauseAllAsync();

        await Mixer.StopEffectAsync(id!);
        await Mixer.ResumeAllAsync();

        ChannelOf(Trueno).Should().BeNull();
    }

    [Fact]
    public async Task Closing_a_loop_while_paused_does_not_sound_on_resume()
    {
        var id = await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);
        await Mixer.PauseAllAsync();

        await Mixer.SetEffectRepeatAsync(id!, EffectRepeat.WithPause(3, 3));
        await Mixer.ResumeAllAsync();

        (ChannelOf(Lluvia), Mixer.State.Effects.Single().Waiting).Should().Be((null, true));
    }

    [Fact]
    public async Task Changing_a_loop_to_repeat_with_pause_closes_the_loop_and_waits()
    {
        var id = await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);

        await Mixer.SetEffectRepeatAsync(id!, EffectRepeat.WithPause(3, 3));
        Advance(0.5);
        var stopped = ChannelOf(Lluvia) is null;
        Advance(3);

        (stopped, ChannelOf(Lluvia)?.Loop).Should().Be((true, false));
    }

    [Fact]
    public async Task Missing_file_is_removed_without_throwing()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);

        Player.RaiseFailed(ChannelIdOf(Fondo));
        Player.RaiseFailed(ChannelIdOf(Trueno));

        (Mixer.State.Background, Mixer.State.Effects.Count).Should().Be((null, 0));
    }

    [Fact]
    public async Task Effect_the_player_rejects_is_not_added()
    {
        Player.MissingPaths.Add(AudioPaths.ToVaultPath(Trueno));

        var id = await Mixer.PlayEffectAsync(Trueno);

        (id, Mixer.State.Effects.Count).Should().Be((null, 0));
    }

    // ---- Volumen general, pausa y parada ----

    [Fact]
    public async Task Master_volume_scales_every_channel()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);
        var effect = await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);
        await Mixer.SetEffectVolumeAsync(effect!, 0.6);

        await Mixer.SetMasterVolumeAsync(0.5);

        (VolumeOf(Fondo), VolumeOf(Trueno)).Should().Be((0.4, 0.3));
    }

    [Fact]
    public async Task Stop_all_fades_out_and_then_stops_every_channel()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        Advance(2);
        await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);

        await Mixer.StopAllAsync();
        Advance(0.25);
        var halfway = (VolumeOf(Fondo), VolumeOf(Trueno));
        Advance(0.25);

        (halfway, Player.Channels.Count, Mixer.IsPlaying).Should().Be(((0.4, 0.4), 0, false));
    }

    [Fact]
    public async Task Stop_all_ends_with_a_stop_all_to_the_player()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);

        await Mixer.StopAllAsync();
        Advance(0.5);

        Player.StopAllCalls.Should().Be(1);
    }

    [Fact]
    public async Task Stopping_one_effect_while_the_background_plays_does_not_stop_all()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        var id = await Mixer.PlayEffectAsync(Trueno, EffectRepeat.Loop);

        await Mixer.StopEffectAsync(id!);
        Advance(0.5);

        Player.StopAllCalls.Should().Be(0);
    }

    [Fact]
    public async Task Playing_while_paused_resumes_the_mix()
    {
        await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);
        await Mixer.PauseAllAsync();

        await Mixer.PlayEffectAsync(Trueno);

        (Mixer.State.Paused, ChannelOf(Lluvia)!.Paused).Should().Be((false, false));
    }

    [Fact]
    public async Task External_pause_and_stop_update_state()
    {
        await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);

        Player.RaiseExternal(AudioExternalAction.Pause);
        var paused = Mixer.State.Paused;
        Player.RaiseExternal(AudioExternalAction.Stop);

        (paused, Mixer.State.Paused, Mixer.State.Effects.Count).Should().Be((true, false, 0));
    }

    [Fact]
    public async Task External_pause_twice_is_harmless()
    {
        await Mixer.PlayEffectAsync(Cuervo, EffectRepeat.WithPause(5, 5));
        Player.RaiseEnded(ChannelIdOf(Cuervo));

        Player.RaiseExternal(AudioExternalAction.Pause);
        Advance(10);
        Player.RaiseExternal(AudioExternalAction.Pause);
        Advance(10);
        Player.RaiseExternal(AudioExternalAction.Resume);
        Advance(4.9);
        var early = ChannelOf(Cuervo) is not null;
        Advance(0.1);

        (early, Mixer.State.Paused, ChannelOf(Cuervo) is not null).Should().Be((false, false, true));
    }

    [Fact]
    public async Task External_pause_with_an_empty_mix_is_resumed_by_the_next_effect()
    {
        Player.RaiseExternal(AudioExternalAction.Pause);
        var paused = Mixer.State.Paused;

        await Mixer.PlayEffectAsync(Trueno);

        (paused, Mixer.State.Paused, ChannelOf(Trueno) is not null).Should().Be((true, false, true));
    }

    [Fact]
    public async Task Vault_switch_stops_everything()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);

        await Host.OpenAsync(OtherVaultRoot);

        (Player.StopAllCalls, Mixer.State.Background, Mixer.State.Effects.Count).Should().Be((1, null, 0));
    }

    [Fact]
    public async Task Disposing_stops_everything_and_ignores_later_orders()
    {
        await Mixer.PlayEffectAsync(Lluvia, EffectRepeat.Loop);

        await Mixer.DisposeAsync();
        var id = await Mixer.PlayEffectAsync(Trueno);

        (Player.StopAllCalls, Player.Channels.Count, id, Mixer.State.Effects.Count).Should().Be((1, 0, (string?)null, 0));
    }

    [Fact]
    public async Task Changed_is_raised_after_an_order()
    {
        var raised = 0;
        Mixer.Changed += (_, _) => raised++;

        await Mixer.PlayEffectAsync(Trueno);

        raised.Should().Be(1);
    }
}
