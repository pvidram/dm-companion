using DmCompanion.Core.Audio;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Components.Sound;

/// <summary>Base de los controles rápidos de sonido (mini control, panel rápido, botón de ambiente): se repintan cuando cambia la mezcla, la biblioteca o las escenas.</summary>
public abstract class SoundControlBase : DmComponentBase
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(6);

    private ITimer? _noticeTimer;

    [Inject] protected AudioMixer Mixer { get; set; } = default!;
    [Inject] protected AudioStore Store { get; set; } = default!;
    [Inject] protected AudioLibraryService Library { get; set; } = default!;
    [Inject] private TimeProvider Time { get; set; } = default!;
    [Inject] private ILogger<SoundControlBase> Logger { get; set; } = default!;

    protected AudioLibrary Tracks => Library.Current;

    /// <summary>Hay algo sonando o en pausa.</summary>
    protected bool HasMix => Mixer.IsPlaying || Mixer.State.Paused;

    /// <summary>Aviso local que caduca solo (algo que no se pudo lanzar).</summary>
    protected string? Notice { get; private set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Mixer.Changed += OnMixChanged;
        Store.Changed += OnCatalogChanged;
        Library.Changed += OnCatalogChanged;
    }

    /// <summary>Cambió la biblioteca o las escenas (no la mezcla).</summary>
    protected virtual void OnCatalogChangedCore() { }

    protected void ShowNotice(string text)
    {
        Notice = text;
        _noticeTimer?.Dispose();
        _noticeTimer = Time.CreateTimer(_ => ExpireNotice(), null, NoticeLifetime, Timeout.InfiniteTimeSpan);
    }

    private void ExpireNotice()
    {
        Notice = null;
        _ = RefreshSoundAsync(catalog: false);
    }

    private void OnMixChanged(object? sender, EventArgs e) => _ = RefreshSoundAsync(catalog: false);

    private void OnCatalogChanged(object? sender, EventArgs e) => _ = RefreshSoundAsync(catalog: true);

    // Los servicios avisan desde otros hilos.
    private async Task RefreshSoundAsync(bool catalog)
    {
        try
        {
            await InvokeAsync(() =>
            {
                if (catalog) OnCatalogChangedCore();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // El componente ya se retiró mientras llegaba el evento.
        }
        catch (Exception ex)
        {
            Logger.LogError("Fallo al refrescar {Component} tras un cambio de sonido ({ExceptionType}).", GetType().Name, ex.GetType().FullName);
        }
    }

    public override void Dispose()
    {
        Mixer.Changed -= OnMixChanged;
        Store.Changed -= OnCatalogChanged;
        Library.Changed -= OnCatalogChanged;
        _noticeTimer?.Dispose();
        base.Dispose();
    }
}
