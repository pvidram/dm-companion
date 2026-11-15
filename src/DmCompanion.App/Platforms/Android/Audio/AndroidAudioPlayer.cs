using Android.Media;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

/// <summary>
/// Reproductor nativo: un <see cref="MediaPlayer"/> por canal, abierto por ruta absoluta dentro del vault.
/// Mientras suena algo mantiene el foco de audio y el servicio en primer plano (<see cref="AudioPlaybackSession"/>).
/// </summary>
public sealed class AndroidAudioPlayer : IAudioPlayer
{
    private readonly VaultHost vaultHost;
    private readonly ILogger<AndroidAudioPlayer> logger;
    private readonly AudioPlaybackSession session;
    private readonly AudioFocusHandler focus;
    private readonly Lock gate = new();
    private readonly Dictionary<string, Channel> channels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> channelGenerations = new(StringComparer.Ordinal);
    private long globalGeneration;
    private bool paused;

    public AndroidAudioPlayer(VaultHost vaultHost, ILogger<AndroidAudioPlayer> logger)
    {
        this.vaultHost = vaultHost;
        this.logger = logger;
        session = new AudioPlaybackSession(logger);
        focus = new AudioFocusHandler(Android.App.Application.Context, OnFocusLost);
    }

    /// <summary>Estado global de pausa, para la notificación.</summary>
    public bool IsPaused
    {
        get { lock (gate) return paused; }
    }

    public event EventHandler<string>? Ended;
    public event EventHandler<string>? Failed;
    public event EventHandler<AudioExternalAction>? ExternalAction;

    public async Task<bool> PlayAsync(string channelId, string vaultRelativePath, double volume, bool loop)
    {
        var (previous, _, ticket) = RemoveChannel(channelId);
        Release(previous);
        if (ResolvePath(vaultRelativePath) is not { } absolutePath) return NotPlayed(false);

        var player = await Task.Run(() => Prepare(absolutePath, vaultRelativePath, volume, loop));
        if (player is null) return NotPlayed(false);

        var channel = new Channel(player, vaultRelativePath);
        player.Completion += (_, _) => OnCompleted(channelId, channel);
        player.Error += (_, e) =>
        {
            e.Handled = true;
            OnFailed(channelId, channel, $"MediaPlayer error {e.What}");
        };

        bool superseded, startPaused;
        lock (gate)
        {
            // Un Stop/StopAll/Play del mismo canal llegado mientras se preparaba gana: este no debe sonar.
            superseded = ticket != (globalGeneration, ChannelGeneration(channelId));
            if (!superseded) channels[channelId] = channel;
            startPaused = paused;
        }

        if (superseded)
        {
            Release(channel);
            return NotPlayed(true);
        }

        // Con todo en pausa el canal nace en pausa: reanudar lo decide quien llame a ResumeAllAsync.
        if (!startPaused)
        {
            if (focus.Request())
            {
                player.Start();
            }
            else
            {
                // Sin foco (p. ej. la mezcla relanza un efecto durante una llamada): todo queda en pausa.
                lock (gate) paused = startPaused = true;
                logger.LogInformation("Reproducción en pausa: sin foco de audio.");
                RaiseDeferred(AudioExternalAction.Pause);
            }
        }

        session.Show(startPaused);
        return true;
    }

    /// <summary>Salida de PlayAsync sin canal nuevo: si no queda nada sonando, suelta foco y servicio.</summary>
    private bool NotPlayed(bool result)
    {
        bool empty;
        lock (gate) empty = channels.Count == 0;
        if (empty) GoIdle();
        return result;
    }

    /// <summary>Aviso en el hilo principal, aparte, para no reentrar en la mezcla mientras espera la llamada.</summary>
    private void RaiseDeferred(AudioExternalAction action) =>
        MainThread.BeginInvokeOnMainThread(() => ExternalAction?.Invoke(this, action));

    public Task SetVolumeAsync(string channelId, double volume)
    {
        Channel? channel;
        lock (gate) channels.TryGetValue(channelId, out channel);
        if (channel is not null) Run(channel, () => channel.Player.SetVolume(Clamp(volume), Clamp(volume)));
        return Task.CompletedTask;
    }

    public Task PauseAllAsync()
    {
        var pausedChannels = Snapshot(markPaused: true);
        foreach (var channel in pausedChannels)
            Run(channel, () => { if (channel.Player.IsPlaying) channel.Player.Pause(); });
        if (pausedChannels.Count > 0) session.Show(paused: true);
        return Task.CompletedTask;
    }

    public Task ResumeAllAsync()
    {
        // Sin foco (llamada en curso) no se suena encima: sigue en pausa y la mezcla se entera.
        if (!TryResumeAll()) RaiseDeferred(AudioExternalAction.Pause);
        return Task.CompletedTask;
    }

    public Task StopAllAsync()
    {
        List<Channel> stopped;
        lock (gate)
        {
            stopped = [.. channels.Values];
            channels.Clear();
            channelGenerations.Clear();
            globalGeneration++;
            paused = false;
        }

        foreach (var channel in stopped) Release(channel);
        focus.Abandon();
        session.StopNow();
        return Task.CompletedTask;
    }

    /// <summary>Acción de la notificación o del sistema: la aplica y avisa a la mezcla.</summary>
    public async Task HandleExternalActionAsync(AudioExternalAction action)
    {
        try
        {
            if (action == AudioExternalAction.Resume)
            {
                // Denegado: sigue en pausa, que es lo que ya cree la mezcla; no se avisa de nada.
                if (TryResumeAll()) ExternalAction?.Invoke(this, action);
                return;
            }

            await (action == AudioExternalAction.Pause ? PauseAllAsync() : StopAllAsync());
            ExternalAction?.Invoke(this, action);
        }
        catch (Exception ex)
        {
            logger.LogError("No se pudo aplicar la acción externa {Action}: {Error}", action, ex.GetType().Name);
        }
    }

    public ValueTask DisposeAsync() => new(StopAllAsync());

    /// <summary>Reanuda todos los canales; false si no hay foco de audio y se queda en pausa.</summary>
    private bool TryResumeAll()
    {
        bool any;
        lock (gate)
        {
            any = channels.Count > 0;
            if (!any) paused = false;
        }

        if (!any) return true;

        if (!focus.Request())
        {
            logger.LogInformation("Reanudar denegado: sin foco de audio.");
            return false;
        }

        foreach (var channel in Snapshot(markPaused: false)) Run(channel, () => channel.Player.Start());
        session.Show(paused: false);
        return true;
    }

    private void OnFocusLost()
    {
        bool playing;
        lock (gate) playing = channels.Count > 0 && !paused;
        if (playing) _ = HandleExternalActionAsync(AudioExternalAction.Pause);
    }

    private string? ResolvePath(string vaultRelativePath)
    {
        if (vaultHost.Current?.RootPath is not { } root) return null;
        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(rootFull, vaultRelativePath));
        var inside = full.StartsWith(Path.TrimEndingDirectorySeparator(rootFull) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (inside && File.Exists(full)) return full;

        logger.LogWarning("Audio no encontrado: {Path}", vaultRelativePath);
        return null;
    }

    private MediaPlayer? Prepare(string absolutePath, string vaultRelativePath, double volume, bool loop)
    {
        var player = new MediaPlayer();
        try
        {
            player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Music)!
                .Build()!);
            player.SetWakeMode(Android.App.Application.Context, Android.OS.WakeLockFlags.Partial);
            player.SetDataSource(absolutePath);
            player.Prepare();
            player.Looping = loop;
            player.SetVolume(Clamp(volume), Clamp(volume));
            return player;
        }
        catch (Exception ex)
        {
            logger.LogWarning("No se pudo abrir el audio {Path}: {Error}", vaultRelativePath, ex.GetType().Name);
            player.Release();
            player.Dispose();
            return null;
        }
    }

    public Task StopAsync(string channelId)
    {
        var (channel, empty, _) = RemoveChannel(channelId);
        Release(channel);
        if (channel is not null && empty) GoIdle();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Quita el canal y deja superada cualquier preparación en curso en él. El ticket devuelto identifica
    /// la orden actual: si al terminar de preparar ya no coincide, otra orden ha ganado.
    /// </summary>
    private (Channel? Removed, bool Empty, (long Global, long Channel) Ticket) RemoveChannel(string channelId)
    {
        lock (gate)
        {
            channels.Remove(channelId, out var removed);
            var generation = ChannelGeneration(channelId) + 1;
            channelGenerations[channelId] = generation;
            return (removed, channels.Count == 0, (globalGeneration, generation));
        }
    }

    private void OnCompleted(string channelId, Channel channel)
    {
        if (!Detach(channelId, channel, out var empty)) return;
        Release(channel);
        if (empty) GoIdle();
        Ended?.Invoke(this, channelId);
    }

    private void OnFailed(string channelId, Channel channel, string reason)
    {
        if (!Detach(channelId, channel, out var empty)) return;
        logger.LogWarning("Fallo al reproducir {Path}: {Error}", channel.RelativePath, reason);
        Release(channel);
        if (empty) GoIdle();
        Failed?.Invoke(this, channelId);
    }

    /// <summary>Quita el canal solo si sigue siendo el mismo reproductor (un aviso tardío no borra uno nuevo).</summary>
    private bool Detach(string channelId, Channel channel, out bool empty)
    {
        lock (gate)
        {
            var current = channels.TryGetValue(channelId, out var found) && ReferenceEquals(found, channel);
            if (current) channels.Remove(channelId);
            empty = channels.Count == 0;
            return current;
        }
    }

    private long ChannelGeneration(string channelId) => channelGenerations.GetValueOrDefault(channelId);

    private void GoIdle()
    {
        focus.Abandon();
        session.StopWhenIdle();
    }

    private List<Channel> Snapshot(bool markPaused)
    {
        lock (gate)
        {
            paused = markPaused;
            return [.. channels.Values];
        }
    }

    private void Run(Channel channel, Action action)
    {
        try
        {
            action();
        }
        catch (Java.Lang.IllegalStateException ex)
        {
            logger.LogWarning("Orden ignorada en {Path}: {Error}", channel.RelativePath, ex.GetType().Name);
        }
    }

    private void Release(Channel? channel)
    {
        if (channel is null) return;
        Run(channel, () =>
        {
            if (channel.Player.IsPlaying) channel.Player.Stop();
        });
        channel.Player.Release();
        channel.Player.Dispose();
    }

    private static float Clamp(double volume) => (float)Math.Clamp(volume, 0, 1);

    private sealed record Channel(MediaPlayer Player, string RelativePath);
}
