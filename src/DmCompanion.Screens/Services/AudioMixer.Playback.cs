using DmCompanion.Core.Audio;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Escenas, fondo y efectos (incluidas las esperas de repetición). Todo se llama con el gate tomado.
public sealed partial class AudioMixer
{
    // ---- Escenas ----

    /// <summary>Pasa a la escena según <see cref="SceneTransition"/>; devuelve las rutas (pistas o secciones) que faltan.</summary>
    public async Task<IReadOnlyList<string>> LaunchSceneAsync(AudioScene scene)
    {
        var missing = new List<string>();
        await GuardAsync(async () =>
        {
            await ResumeIfPausedAsync();
            var plan = SceneTransition.Plan(Snapshot(), scene);
            await ApplySceneBackgroundAsync(scene.Background, plan.ChangeBackground, missing);
            foreach (var id in plan.FadeOut)
                if (FindEffect(id) is { } effect) await RemoveEffectAsync(effect, AudioDefaults.Crossfade);
            foreach (var (id, target) in plan.Keep)
                if (FindEffect(id) is { } effect) await RetargetEffectAsync(effect, target);
            foreach (var target in plan.Start)
                if (!await StartSceneEffectAsync(target)) missing.Add(target.Track);
            FadeMaster(scene.MasterVolume);
            _sceneName = scene.Name;
        });
        return missing;
    }

    /// <summary>La mezcla actual como escena: fondo, efectos que se repiten y volumen general. Los de una vez no entran.</summary>
    public AudioScene CaptureScene(string name)
    {
        var (state, master) = _published;
        return new AudioScene(
            name,
            state.Background is { } b ? new SceneBackground(b.Source, b.Volume) : null,
            [.. state.Effects.Where(e => e.Repeat.Kind != RepeatKind.Once).Select(e => new SceneEffect(e.Track, e.Volume, e.Repeat))],
            master);
    }

    private MixSnapshot Snapshot() => new(
        _background?.Source,
        [.. _effects.Select(e => new PlayingEffect(e.Id, e.Track, e.Volume, e.Repeat))]);

    private async Task ApplySceneBackgroundAsync(SceneBackground? target, bool change, List<string> missing)
    {
        if (!change)
        {
            if (target is not null && _background is { } current)
            {
                current.Volume = Math.Clamp(target.Volume, 0, 1);
                await SetLevelAsync(current.ChannelId, current.Volume);
            }
            return;
        }
        if (target is null)
            await DropBackgroundAsync(AudioDefaults.Crossfade);
        else if (!await ReplaceBackgroundAsync(target.Source, Math.Clamp(target.Volume, 0, 1)))
            missing.Add(SourcePath(target.Source));
    }

    private async Task RetargetEffectAsync(MixEffect effect, SceneEffect target)
    {
        effect.Volume = Math.Clamp(target.Volume, 0, 1);
        await SetLevelAsync(effect.ChannelId, effect.Volume);
        await ApplyRepeatAsync(effect, target.Repeat.Normalize());
    }

    private async Task<bool> StartSceneEffectAsync(SceneEffect target)
    {
        if (_library.Current.FindTrack(target.Track) is not { } track) return false;
        var effect = new MixEffect(NewId("fx"), track.Path, Math.Clamp(target.Volume, 0, 1), target.Repeat.Normalize());
        if (!await PlayEffectChannelAsync(effect)) return false;
        _effects.Add(effect);
        return true;
    }

    // ---- Fondo ----

    private IReadOnlyList<string>? ResolveTracks(BackgroundSource source) => source switch
    {
        TrackBackground t => _library.Current.FindTrack(t.Track) is { } track ? [track.Path] : null,
        SectionBackground s => _library.Current.FindSection(s.Section) is { Tracks.Count: > 0 } section
            ? [.. section.Tracks.Select(track => track.Path)]
            : null,
        _ => null,
    };

    private bool CanResolve(BackgroundSource source) => ResolveTracks(source) is not null;

    // El fondo anterior se apaga con fundido cruzado aunque el nuevo falle: la última orden manda.
    private async Task<bool> ReplaceBackgroundAsync(BackgroundSource source, double level)
    {
        var tracks = ResolveTracks(source);
        await DropBackgroundAsync(AudioDefaults.Crossfade);
        if (tracks is null) return false;
        // Con una sola pista, el bucle nativo del reproductor no deja hueco entre vueltas.
        var playlist = tracks.Count > 1 ? new ShufflePlaylist(tracks, _random) : null;
        var background = new MixBackground(source, level, playlist);
        if (!await PlayBackgroundTrackAsync(background, playlist?.Next() ?? tracks[0], gain: 0)) return false;
        FadeChannel(_channels[background.ChannelId!], 1, AudioDefaults.Crossfade, stopAtEnd: false);
        _background = background;
        return true;
    }

    private async Task<bool> PlayBackgroundTrackAsync(MixBackground background, string track, double gain)
    {
        var id = await StartChannelAsync(track, background.Volume, gain, loop: background.Playlist is null);
        (background.ChannelId, background.CurrentTrack) = (id, id is null ? null : track);
        return id is not null;
    }

    private async Task DropBackgroundAsync(TimeSpan fade)
    {
        var channelId = _background?.ChannelId;
        _background = null;
        await ReleaseChannelAsync(channelId, fade);
    }

    private static string SourcePath(BackgroundSource source) => source switch
    {
        TrackBackground t => t.Track,
        SectionBackground s => s.Section,
        _ => "",
    };

    private static bool SameSource(BackgroundSource a, BackgroundSource b) =>
        a.GetType() == b.GetType() && string.Equals(SourcePath(a), SourcePath(b), StringComparison.OrdinalIgnoreCase);

    // ---- Efectos ----

    private async Task<bool> PlayEffectChannelAsync(MixEffect effect)
    {
        effect.ChannelId = await StartChannelAsync(effect.Track, effect.Volume, gain: 1, loop: effect.Repeat.Kind == RepeatKind.Loop);
        return effect.ChannelId is not null;
    }

    private async Task RemoveEffectAsync(MixEffect effect, TimeSpan fade)
    {
        effect.CancelWait();
        _effects.Remove(effect);
        await ReleaseChannelAsync(effect.ChannelId, fade);
    }

    private async Task ApplyRepeatAsync(MixEffect effect, EffectRepeat repeat)
    {
        var previous = effect.Repeat.Kind;
        effect.Repeat = repeat;
        if (effect.Waiting)
        {
            if (repeat.Kind == RepeatKind.RepeatWithPause) return;
            effect.CancelWait();
            if (repeat.Kind == RepeatKind.Once || !await PlayEffectChannelAsync(effect)) _effects.Remove(effect);
            return;
        }
        // El reproductor no quita el bucle de un canal que suena: se cierra con fundido como si hubiera terminado.
        if (previous == RepeatKind.Loop && repeat.Kind != RepeatKind.Loop && effect.ChannelId is { } id)
        {
            effect.ChannelId = null;
            await ReleaseChannelAsync(id, AudioDefaults.StopFade);
            if (repeat.Kind == RepeatKind.Once) _effects.Remove(effect);
            else StartWait(effect);
        }
    }

    private void StartWait(MixEffect effect)
    {
        effect.WaitUntil = ActiveNow + RepeatPause.Next(effect.Repeat, _random);
        if (!Paused) ArmWait(effect);
    }

    private void ArmWait(MixEffect effect)
    {
        var due = effect.WaitUntil!.Value - ActiveNow;
        ITimer? timer = null;
        timer = _time.CreateTimer(_ => Fire(() => WaitElapsedAsync(effect, timer)), null,
            due < TimeSpan.Zero ? TimeSpan.Zero : due, Timeout.InfiniteTimeSpan);
        effect.WaitTimer = timer;
    }

    // Un temporizador viejo (efecto parado, espera rearmada tras una pausa) no hace nada.
    private async Task WaitElapsedAsync(MixEffect effect, ITimer? timer)
    {
        if (Paused || effect.WaitTimer != timer || !_effects.Contains(effect)) return;
        effect.CancelWait();
        if (!await PlayEffectChannelAsync(effect)) _effects.Remove(effect);
    }
}
