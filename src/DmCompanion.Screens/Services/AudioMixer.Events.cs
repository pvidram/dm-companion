using DmCompanion.Core.Audio;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Avisos del reproductor y de los temporizadores, gate y publicación del estado.
public sealed partial class AudioMixer
{
    // ---- Avisos del reproductor ----

    private void OnEnded(object? sender, string channelId) => Fire(() => HandleEndedAsync(channelId));

    private void OnFailed(object? sender, string channelId) => Fire(() =>
    {
        HandleFailed(channelId);
        return Task.CompletedTask;
    });

    private void OnExternalAction(object? sender, AudioExternalAction action) => Fire(() =>
    {
        // El reproductor ya ha pausado, reanudado o parado: aquí solo se pone el estado al día.
        if (action == AudioExternalAction.Pause && !Paused) Freeze();
        else if (action == AudioExternalAction.Resume && Paused) Unfreeze();
        else if (action == AudioExternalAction.Stop) ClearMix();
        return Task.CompletedTask;
    });

    private async Task HandleEndedAsync(string channelId)
    {
        if (!_channels.Remove(channelId, out var ended)) return;
        if (_background is { } background && background.ChannelId == channelId)
        {
            await PlayNextBackgroundTrackAsync(background, ended);
            return;
        }
        if (_effects.FirstOrDefault(e => e.ChannelId == channelId) is not { } effect) return;
        effect.ChannelId = null;
        switch (effect.Repeat.Kind)
        {
            case RepeatKind.Once:
                _effects.Remove(effect);
                break;
            case RepeatKind.Loop:
                if (!await PlayEffectChannelAsync(effect)) _effects.Remove(effect);
                break;
            case RepeatKind.RepeatWithPause:
                StartWait(effect);
                break;
        }
    }

    // La pista siguiente hereda la ganancia y el fundido de entrada en curso de la que acaba (pistas más cortas que el fundido).
    private async Task PlayNextBackgroundTrackAsync(MixBackground background, MixChannel ended)
    {
        background.ChannelId = null;
        if (background.Playlist is not { } playlist || !await PlayBackgroundTrackAsync(background, playlist.Next()!, ended.Gain))
        {
            _background = null;
            return;
        }
        if (ended.Fade is { } fade) _channels[background.ChannelId!].Fade = fade;
    }

    private void HandleFailed(string channelId)
    {
        if (!_channels.Remove(channelId, out var channel)) return;
        _logger.LogWarning("No se pudo reproducir {Path}; se quita de la mezcla", channel.VaultPath);
        if (_background?.ChannelId == channelId) _background = null;
        _effects.RemoveAll(e => e.ChannelId == channelId);
    }

    // ---- Infraestructura ----

    private Task GuardAsync(Func<Task> body) => GuardAsync(async () =>
    {
        await body();
        return true;
    });

    // Ejecuta bajo el gate y publica el estado; avisa fuera del gate si el cuerpo lo pide. Tras desechar no hace nada.
    // El estado se publica aunque el cuerpo lance, para que State no se quede atrás del estado interno.
    private async Task GuardAsync(Func<Task<bool>> body)
    {
        bool notify;
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            notify = await body();
        }
        finally
        {
            if (!_disposed) Publish();
            _gate.Release();
        }
        if (notify) RaiseChanged();
    }

    // Los avisos del reproductor y de los temporizadores no esperan a nadie: un fallo solo se registra (tipo, sin mensaje).
    private void Fire(Func<Task> body) => Fire(async () =>
    {
        await body();
        return true;
    });

    private void Fire(Func<Task<bool>> body) => _ = FireAsync(body);

    private async Task FireAsync(Func<Task<bool>> body)
    {
        try
        {
            await GuardAsync(body);
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo en un aviso del mezclador de sonido ({ExceptionType})", ex.GetType().Name);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError("Un suscriptor de AudioMixer.Changed ha lanzado una excepción ({ExceptionType})", ex.GetType().Name);
        }
    }

    private void Publish() => _published = new Published(
        new MixState(
            _background is { } b ? new BackgroundState(b.Source, b.CurrentTrack, b.Volume) : null,
            [.. _effects.Select(e => new EffectState(e.Id, e.Track, e.Volume, e.Repeat, e.Waiting))],
            _master,
            Paused,
            _sceneName),
        _masterFade?.To ?? _master);

    private sealed record Published(MixState State, double SceneMaster);

    private string NewId(string prefix) => $"{prefix}{++_nextId}";

    private MixEffect? FindEffect(string id) => _effects.FirstOrDefault(e => e.Id == id);
}
