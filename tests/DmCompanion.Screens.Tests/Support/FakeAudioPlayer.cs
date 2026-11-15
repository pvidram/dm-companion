using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Tests.Support;

/// <summary>
/// Reproductor falso: guarda el estado de cada canal y deja a los tests disparar eventos. Cumple el contrato de pausa
/// global de <see cref="IAudioPlayer"/>: un canal que nace en pausa queda en pausa hasta <see cref="ResumeAllAsync"/>.
/// </summary>
public sealed class FakeAudioPlayer : IAudioPlayer
{
    public sealed record Channel(string Path, double Volume, bool Loop, bool Paused);

    public Dictionary<string, Channel> Channels { get; } = [];

    /// <summary>Rutas que <see cref="PlayAsync"/> rechaza (devuelve false).</summary>
    public HashSet<string> MissingPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int StopAllCalls { get; private set; }

    public bool GloballyPaused { get; private set; }

    public event EventHandler<string>? Ended;

    public event EventHandler<string>? Failed;

    public event EventHandler<AudioExternalAction>? ExternalAction;

    public Task<bool> PlayAsync(string channelId, string vaultRelativePath, double volume, bool loop)
    {
        if (MissingPaths.Contains(vaultRelativePath))
        {
            Channels.Remove(channelId);
            return Task.FromResult(false);
        }

        Channels[channelId] = new Channel(vaultRelativePath, volume, loop, Paused: GloballyPaused);
        return Task.FromResult(true);
    }

    public Task SetVolumeAsync(string channelId, double volume)
    {
        if (Channels.TryGetValue(channelId, out var channel))
            Channels[channelId] = channel with { Volume = volume };
        return Task.CompletedTask;
    }

    public Task StopAsync(string channelId)
    {
        Channels.Remove(channelId);
        return Task.CompletedTask;
    }

    public Task PauseAllAsync() => SetPaused(true);

    public Task ResumeAllAsync() => SetPaused(false);

    public Task StopAllAsync()
    {
        StopAllCalls++;
        GloballyPaused = false;
        Channels.Clear();
        return Task.CompletedTask;
    }

    public void RaiseEnded(string channelId)
    {
        Channels.Remove(channelId);
        Ended?.Invoke(this, channelId);
    }

    public void RaiseFailed(string channelId)
    {
        Channels.Remove(channelId);
        Failed?.Invoke(this, channelId);
    }

    // Como el reproductor real: primero pausa, reanuda o para, y luego avisa.
    public void RaiseExternal(AudioExternalAction action)
    {
        switch (action)
        {
            case AudioExternalAction.Pause:
                SetPaused(true);
                break;
            case AudioExternalAction.Resume:
                SetPaused(false);
                break;
            case AudioExternalAction.Stop:
                GloballyPaused = false;
                Channels.Clear();
                break;
        }
        ExternalAction?.Invoke(this, action);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task SetPaused(bool paused)
    {
        GloballyPaused = paused;
        foreach (var (id, channel) in Channels.ToList())
            Channels[id] = channel with { Paused = paused };
        return Task.CompletedTask;
    }
}
