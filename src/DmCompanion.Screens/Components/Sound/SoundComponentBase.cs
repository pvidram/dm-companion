using DmCompanion.Screens.Services;
using DmCompanion.UI.Components;
using Microsoft.AspNetCore.Components;

namespace DmCompanion.Screens.Components.Sound;

/// <summary>
/// Base de los bloques de la pantalla «Sonido». Cada bloque declara a qué cambios de audio atiende y se repinta
/// solo por ellos; los avisos que llegan mientras hay un repintado pendiente se funden en uno.
/// </summary>
public abstract class SoundComponentBase : DmComponentBase
{
    [Inject] protected AudioMixer Mixer { get; set; } = default!;
    [Inject] protected AudioLibraryService Library { get; set; } = default!;
    [Inject] protected AudioStore Store { get; set; } = default!;

    [Inject] private TimeProvider Time { get; set; } = default!;

    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(6);

    private int _repaintPending;
    private ITimer? _noticeTimer;

    /// <summary>Aviso visible del bloque; caduca solo a los 6 s o se cierra con ✕.</summary>
    protected string? NoticeText { get; private set; }
    protected NoticeKind NoticeLevel { get; private set; }

    protected virtual bool ListensToMixer => false;
    protected virtual bool ListensToLibrary => false;
    protected virtual bool ListensToStore => false;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (ListensToMixer) Mixer.Changed += OnAudioChanged;
        if (ListensToLibrary) Library.Changed += OnAudioChanged;
        if (ListensToStore) Store.Changed += OnAudioChanged;
    }

    protected void ShowNotice(NoticeKind level, string? text)
    {
        _noticeTimer?.Dispose();
        _noticeTimer = null;
        (NoticeLevel, NoticeText) = (level, text);
        if (text is null) return;
        _noticeTimer = Time.CreateTimer(_ => ExpireNotice(text), null, NoticeLifetime, Timeout.InfiniteTimeSpan);
    }

    protected void ClearNotice() => ShowNotice(NoticeKind.Info, null);

    private void ExpireNotice(string text)
    {
        if (NoticeText != text) return;
        NoticeText = null;
        RequestRepaint();
    }

    public override void Dispose()
    {
        _noticeTimer?.Dispose();
        Mixer.Changed -= OnAudioChanged;
        Library.Changed -= OnAudioChanged;
        Store.Changed -= OnAudioChanged;
        base.Dispose();
    }

    /// <summary>Si el cambio de la mezcla afecta a este bloque; por defecto, siempre que escuche al mezclador.</summary>
    protected virtual bool MixerChangeMatters() => true;

    // Los avisos llegan desde hilos del reproductor, de los temporizadores y del watcher.
    private void OnAudioChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Mixer) && !MixerChangeMatters()) return;
        RequestRepaint();
    }

    private void RequestRepaint()
    {
        if (Interlocked.Exchange(ref _repaintPending, 1) == 0) _ = RepaintAsync();
    }

    private async Task RepaintAsync()
    {
        try
        {
            await InvokeAsync(() =>
            {
                Volatile.Write(ref _repaintPending, 0);
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // El componente se retiró mientras llegaba el aviso.
        }
    }
}
