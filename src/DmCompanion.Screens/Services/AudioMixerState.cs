using DmCompanion.Core.Audio;

namespace DmCompanion.Screens.Services;

/// <summary>Fondo que suena. <paramref name="CurrentTrack"/> es la pista en curso (relativa a <c>_audio/</c>).</summary>
public sealed record BackgroundState(BackgroundSource Source, string? CurrentTrack, double Volume);

/// <summary>Efecto de la mezcla. <paramref name="Waiting"/>: está en la pausa entre dos repeticiones.</summary>
public sealed record EffectState(string Id, string Track, double Volume, EffectRepeat Repeat, bool Waiting);

public sealed record MixState(
    BackgroundState? Background,
    IReadOnlyList<EffectState> Effects,
    double MasterVolume,
    bool Paused,
    string? SceneName)
{
    public static MixState Empty { get; } = new(null, [], 1.0, false, null);
}

/// <summary>Canal del reproductor. Volumen efectivo = <see cref="Level"/> × <see cref="Gain"/> × general.</summary>
internal sealed class MixChannel(string id, string vaultPath, double level, double gain)
{
    public string Id { get; } = id;
    public string VaultPath { get; } = vaultPath;
    public double Level { get; set; } = level;
    public double Gain { get; set; } = gain;
    public GainFade? Fade { get; set; }
}

/// <summary>Fundido de ganancia medido en tiempo activo (sin contar las pausas); al acabar, opcionalmente para el canal.</summary>
internal sealed record GainFade(double From, double To, TimeSpan Start, TimeSpan Duration, bool StopAtEnd);

internal sealed class MixBackground(BackgroundSource source, double volume, ShufflePlaylist? playlist)
{
    public BackgroundSource Source { get; } = source;
    public double Volume { get; set; } = volume;
    public ShufflePlaylist? Playlist { get; } = playlist;
    public string? ChannelId { get; set; }
    public string? CurrentTrack { get; set; }
}

internal sealed class MixEffect(string id, string track, double volume, EffectRepeat repeat)
{
    public string Id { get; } = id;
    public string Track { get; } = track;
    public double Volume { get; set; } = volume;
    public EffectRepeat Repeat { get; set; } = repeat;
    public string? ChannelId { get; set; }

    /// <summary>Fin de la pausa en tiempo activo; null si no está esperando.</summary>
    public TimeSpan? WaitUntil { get; set; }

    public ITimer? WaitTimer { get; set; }

    public bool Waiting => WaitUntil is not null;

    public void CancelWait()
    {
        WaitTimer?.Dispose();
        WaitTimer = null;
        WaitUntil = null;
    }
}
