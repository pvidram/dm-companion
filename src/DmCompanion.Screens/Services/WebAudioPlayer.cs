using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Reproductor de audio del navegador (DevHost y PC) sobre `window.audioPlayer`. Scoped, como el resto de interop.
/// Nunca dispara <see cref="ExternalAction"/>: el navegador no tiene notificación ni foco de audio que lo pida.
/// </summary>
public sealed class WebAudioPlayer(IJSRuntime js, VaultFileUrls urls, VaultHost host, ILogger<WebAudioPlayer> logger) : IAudioPlayer
{
    private DotNetObjectReference<WebAudioPlayer>? _self;
    private bool _disposed;

    public event EventHandler<string>? Ended;

    public event EventHandler<string>? Failed;

#pragma warning disable CS0067 // Parte de la interfaz; el navegador no la usa.
    public event EventHandler<AudioExternalAction>? ExternalAction;
#pragma warning restore CS0067

    public async Task<bool> PlayAsync(string channelId, string vaultRelativePath, double volume, bool loop)
    {
        if (_disposed) return false;
        var url = await UrlOfAsync(vaultRelativePath);
        if (url is null || _disposed) return false;
        _self ??= DotNetObjectReference.Create(this);
        var outcome = await TryInvokeAsync<string>("audioPlayer.play", channelId, url, volume, loop, _self);
        if (outcome is "ok" or "superseded") return true;
        // Un único camino de fallo: el arranque que falla devuelve false aquí y no dispara Failed.
        // NotAllowedError es el bloqueo del navegador por falta de gesto del usuario; el resto, un fichero que no se reproduce.
        logger.LogWarning("El navegador no pudo reproducir un canal de audio ({ErrorType}).", outcome ?? "JsUnavailable");
        return false;
    }

    public async Task SetVolumeAsync(string channelId, double volume) => await TryInvokeAsync<object>("audioPlayer.setVolume", channelId, volume);

    public async Task StopAsync(string channelId) => await TryInvokeAsync<object>("audioPlayer.stop", channelId);

    public async Task PauseAllAsync() => await TryInvokeAsync<object>("audioPlayer.pauseAll");

    public async Task ResumeAllAsync() => await TryInvokeAsync<object>("audioPlayer.resumeAll");

    public async Task StopAllAsync() => await TryInvokeAsync<object>("audioPlayer.stopAll");

    [JSInvokable]
    public void OnEnded(string channelId)
    {
        if (!_disposed) Ended?.Invoke(this, channelId);
    }

    [JSInvokable]
    public void OnFailed(string channelId)
    {
        if (!_disposed) Failed?.Invoke(this, channelId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAllAsync();
        _disposed = true;
        _self?.Dispose();
        _self = null;
    }

    // Un fichero que falta o una ruta insegura no llegan al navegador: se tratan como «no se puede reproducir».
    private async Task<string?> UrlOfAsync(string path)
    {
        try
        {
            if (host.Current is not { } vault || await vault.StatAsync(path) is not { } info) return null;
            return urls.UrlFor(path, info.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo leer la fecha de un fichero de audio ({ExceptionType}).", ex.GetType().FullName);
            return null;
        }
    }

    private async Task<T?> TryInvokeAsync<T>(string identifier, params object?[] args)
    {
        try
        {
            return await js.InvokeAsync<T>(identifier, args);
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró.
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning("Falló la llamada {Identifier} al reproductor de audio ({ExceptionType}).", identifier, ex.GetType().FullName);
        }
        return default;
    }
}
