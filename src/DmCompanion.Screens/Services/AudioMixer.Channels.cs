using DmCompanion.Core.Audio;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Canales, fundidos, pausa y parada en seco. Todo se llama con el gate tomado.
public sealed partial class AudioMixer
{
    // ---- Canales y fundidos ----

    private async Task<string?> StartChannelAsync(string track, double level, double gain, bool loop)
    {
        var channel = new MixChannel(NewId("ch"), AudioPaths.ToVaultPath(track), level, gain);
        if (!await _player.PlayAsync(channel.Id, channel.VaultPath, Effective(channel), loop))
        {
            _logger.LogWarning("No se pudo reproducir {Path}", channel.VaultPath);
            return null;
        }
        _channels[channel.Id] = channel;
        return channel.Id;
    }

    private double Effective(MixChannel channel) => channel.Level * channel.Gain * _master;

    private async Task SetLevelAsync(string? channelId, double level)
    {
        if (channelId is null || !_channels.TryGetValue(channelId, out var channel)) return;
        channel.Level = level;
        await _player.SetVolumeAsync(channelId, Effective(channel));
    }

    // Quita un canal de la mezcla: con fundido si suena; en pausa, en seco (un fundido en pausa no avanza y sonaría al reanudar).
    private async Task ReleaseChannelAsync(string? channelId, TimeSpan fade)
    {
        if (channelId is null || !_channels.TryGetValue(channelId, out var channel)) return;
        if (!Paused)
        {
            FadeChannel(channel, 0, fade, stopAtEnd: true);
            return;
        }
        _channels.Remove(channelId);
        await _player.StopAsync(channelId);
    }

    private void FadeChannel(MixChannel channel, double to, TimeSpan duration, bool stopAtEnd)
    {
        channel.Fade = new GainFade(channel.Gain, to, ActiveNow, duration, stopAtEnd);
        EnsureTicker();
    }

    private void FadeMaster(double to)
    {
        _masterFade = new GainFade(_master, Math.Clamp(to, 0, 1), ActiveNow, AudioDefaults.Crossfade, StopAtEnd: false);
        EnsureTicker();
    }

    private void EnsureTicker()
    {
        if (_ticker is null && !Paused)
            _ticker = _time.CreateTimer(_ => QueueTick(), null, AudioDefaults.FadeStep, AudioDefaults.FadeStep);
    }

    // Como mucho un paso esperando el gate: si una orden lo retiene, los pasos no se acumulan (cada paso se calcula con el tiempo).
    private void QueueTick()
    {
        if (Interlocked.Exchange(ref _tickQueued, 1) == 0) Fire(TickAsync);
    }

    // Un paso de todos los fundidos. Solo el volumen general es visible en el estado, y de él se avisa como mucho cada
    // FadeNoticeInterval y al terminar, para no repintar la UI en cada paso.
    private async Task<bool> TickAsync()
    {
        Volatile.Write(ref _tickQueued, 0);
        if (Paused) return false;
        var now = ActiveNow;
        var masterMoved = _masterFade is not null;
        var stoppedAny = false;
        if (_masterFade is { } master)
        {
            _master = Fade.VolumeAt(master.From, master.To, now - master.Start, master.Duration);
            if (now - master.Start >= master.Duration) _masterFade = null;
        }
        foreach (var channel in _channels.Values.ToList())
        {
            if (channel.Fade is { } fade)
            {
                channel.Gain = Fade.VolumeAt(fade.From, fade.To, now - fade.Start, fade.Duration);
                if (now - fade.Start >= fade.Duration)
                {
                    channel.Fade = null;
                    if (fade.StopAtEnd)
                    {
                        _channels.Remove(channel.Id);
                        await _player.StopAsync(channel.Id);
                        stoppedAny = true;
                        continue;
                    }
                }
            }
            else if (!masterMoved) continue;
            await _player.SetVolumeAsync(channel.Id, Effective(channel));
        }
        if (_masterFade is null && _channels.Values.All(c => c.Fade is null)) StopTicker();
        if (stoppedAny && MixIsEmpty) await _player.StopAllAsync();
        return masterMoved && ShouldNotifyFade(finished: _masterFade is null);
    }

    // Si un fundido de salida deja la mezcla vacía («Parar todo»), el reproductor lo sabe con StopAll: en Android quita
    // la notificación y el servicio al momento en vez de esperar el margen de inactividad.
    private bool MixIsEmpty => _channels.Count == 0 && _effects.Count == 0 && _background is null;

    private bool ShouldNotifyFade(bool finished)
    {
        var now = _time.GetUtcNow();
        if (!finished && now - _lastFadeNotice < FadeNoticeInterval) return false;
        _lastFadeNotice = now;
        return true;
    }

    private void StopTicker()
    {
        _ticker?.Dispose();
        _ticker = null;
    }

    // ---- Pausa y parada ----

    private void Freeze()
    {
        _pausedAt = _time.GetUtcNow();
        StopTicker();
        foreach (var effect in _effects)
        {
            effect.WaitTimer?.Dispose();
            effect.WaitTimer = null;
        }
    }

    private void Unfreeze()
    {
        _pausedTotal += _time.GetUtcNow() - _pausedAt!.Value;
        _pausedAt = null;
        if (_masterFade is not null || _channels.Values.Any(c => c.Fade is not null)) EnsureTicker();
        foreach (var effect in _effects.Where(e => e.Waiting))
            ArmWait(effect);
    }

    private async Task ResumeIfPausedAsync()
    {
        if (!Paused) return;
        await _player.ResumeAllAsync();
        Unfreeze();
    }

    // Parada en seco (cambio de vault, cierre): sin fundidos, todo fuera del reproductor.
    private async Task StopNowAsync()
    {
        ClearMix();
        await _player.StopAllAsync();
    }

    private void ClearMix()
    {
        if (_pausedAt is { } pausedAt) _pausedTotal += _time.GetUtcNow() - pausedAt;
        _pausedAt = null;
        StopTicker();
        foreach (var effect in _effects) effect.CancelWait();
        _effects.Clear();
        _channels.Clear();
        _background = null;
        _master = _masterFade?.To ?? _master;
        _masterFade = null;
        _sceneName = null;
    }
}
