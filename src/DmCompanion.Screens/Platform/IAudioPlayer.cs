namespace DmCompanion.Screens.Platform;

/// <summary>
/// Reproductor de audio de la plataforma: solo carga y reproduce ficheros por canal con el volumen indicado.
/// Fundidos, listas y escenas viven en <c>AudioMixer</c>.
/// </summary>
public interface IAudioPlayer : IAsyncDisposable
{
    /// <summary>
    /// Reproduce <paramref name="vaultRelativePath"/> en el canal; false si el fichero no se puede reproducir.
    /// Si el reproductor está en pausa global, el canal nuevo nace en pausa y suena al reanudar.
    /// </summary>
    /// <param name="volume">Volumen efectivo (pista × general), de 0 a 1.</param>
    Task<bool> PlayAsync(string channelId, string vaultRelativePath, double volume, bool loop);

    Task SetVolumeAsync(string channelId, double volume);

    Task StopAsync(string channelId);

    Task PauseAllAsync();

    Task ResumeAllAsync();

    Task StopAllAsync();

    /// <summary>Un canal sin bucle ha terminado.</summary>
    event EventHandler<string>? Ended;

    /// <summary>Un canal no se pudo reproducir.</summary>
    event EventHandler<string>? Failed;

    /// <summary>Pausa, reanudación o parada pedida desde fuera de la app (notificación, foco de audio).</summary>
    event EventHandler<AudioExternalAction>? ExternalAction;
}

public enum AudioExternalAction { Pause, Resume, Stop }
