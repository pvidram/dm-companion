using DmCompanion.Core.Audio;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Estado de la mezcla (fondo, efectos, volumen general, pausa) y sus temporizadores: fundidos, listas y pausas de repetición.
/// Manda órdenes mínimas por canal a <see cref="IAudioPlayer"/>. Las órdenes, los avisos del reproductor y los temporizadores
/// se serializan con un gate asíncrono (las llamadas al reproductor se esperan dentro); <see cref="Changed"/> salta fuera.
/// </summary>
public sealed partial class AudioMixer : IAsyncDisposable
{
    private static readonly TimeSpan FadeNoticeInterval = TimeSpan.FromMilliseconds(250);

    private readonly IAudioPlayer _player;
    private readonly VaultHost _host;
    private readonly Func<Task> _beforeSwitch;
    private readonly AudioLibraryService _library;
    private readonly TimeProvider _time;
    private readonly ILogger<AudioMixer> _logger;
    private readonly Random _random;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DateTimeOffset _origin;

    private readonly Dictionary<string, MixChannel> _channels = [];
    private readonly List<MixEffect> _effects = [];
    private MixBackground? _background;
    private double _master = 1.0;
    private GainFade? _masterFade;
    private string? _sceneName;
    private DateTimeOffset? _pausedAt;
    private TimeSpan _pausedTotal;
    private ITimer? _ticker;
    private int _tickQueued;
    private DateTimeOffset _lastFadeNotice = DateTimeOffset.MinValue;
    private int _nextId;
    private bool _disposed;
    // Estado publicado junto con el volumen general que guardaría una escena (el destino del fundido en curso, si lo hay):
    // se leen fuera del gate y tienen que ser de la misma publicación.
    private volatile Published _published = new(MixState.Empty, 1.0);

    public AudioMixer(IAudioPlayer player, AudioLibraryService library, VaultHost host, TimeProvider time, ILogger<AudioMixer> logger, Random? random = null)
    {
        (_player, _library, _time, _logger, _random) = (player, library, time, logger, random ?? Random.Shared);
        _origin = time.GetUtcNow();
        _player.Ended += OnEnded;
        _player.Failed += OnFailed;
        _player.ExternalAction += OnExternalAction;
        _host = host;
        _beforeSwitch = () => GuardAsync(StopNowAsync);
        host.AddBeforeSwitch(_beforeSwitch);
    }

    public MixState State => _published.State;

    public bool IsPlaying => State is { Paused: false } s && (s.Background is not null || s.Effects.Count > 0);

    public event EventHandler? Changed;

    private bool Paused => _pausedAt is not null;

    // Tiempo que ha corrido sin pausa: fundidos y esperas se miden con él, así que la pausa los congela.
    private TimeSpan ActiveNow => (_pausedAt ?? _time.GetUtcNow()) - _origin - _pausedTotal;

    // ---- Efectos ----

    /// <summary>Lanza un efecto (una vez por defecto); null si la pista no existe o no se puede reproducir.</summary>
    public async Task<string?> PlayEffectAsync(string track, EffectRepeat? repeat = null)
    {
        if (_library.Current.FindTrack(track) is not { } found) return null;
        string? id = null;
        await GuardAsync(async () =>
        {
            await ResumeIfPausedAsync();
            var effect = new MixEffect(NewId("fx"), found.Path, AudioDefaults.DefaultVolume, (repeat ?? EffectRepeat.Once).Normalize());
            if (!await PlayEffectChannelAsync(effect)) return;
            _effects.Add(effect);
            id = effect.Id;
        });
        return id;
    }

    public Task SetEffectVolumeAsync(string id, double volume) => GuardAsync(async () =>
    {
        if (FindEffect(id) is not { } effect) return;
        effect.Volume = Math.Clamp(volume, 0, 1);
        await SetLevelAsync(effect.ChannelId, effect.Volume);
    });

    public Task SetEffectRepeatAsync(string id, EffectRepeat repeat) => GuardAsync(async () =>
    {
        if (FindEffect(id) is { } effect) await ApplyRepeatAsync(effect, repeat.Normalize());
    });

    public Task StopEffectAsync(string id) => GuardAsync(async () =>
    {
        if (FindEffect(id) is { } effect) await RemoveEffectAsync(effect, AudioDefaults.StopFade);
    });

    // ---- Fondo ----

    /// <summary>Cambia el fondo con fundido cruzado; false si la pista o la sección no existe o está vacía.</summary>
    public async Task<bool> SetBackgroundAsync(BackgroundSource source, double? volume = null)
    {
        if (!CanResolve(source)) return false;
        var ok = false;
        await GuardAsync(async () =>
        {
            await ResumeIfPausedAsync();
            var level = Math.Clamp(volume ?? _background?.Volume ?? AudioDefaults.DefaultVolume, 0, 1);
            // El mismo fondo solo cambia de volumen: la escena sigue siendo la que suena.
            if (_background is { } current && SameSource(current.Source, source))
            {
                current.Volume = level;
                await SetLevelAsync(current.ChannelId, level);
                ok = true;
                return;
            }
            _sceneName = null;
            ok = await ReplaceBackgroundAsync(source, level);
        });
        return ok;
    }

    public Task SetBackgroundVolumeAsync(double volume) => GuardAsync(async () =>
    {
        if (_background is not { } background) return;
        background.Volume = Math.Clamp(volume, 0, 1);
        await SetLevelAsync(background.ChannelId, background.Volume);
    });

    public Task StopBackgroundAsync() => GuardAsync(async () =>
    {
        _sceneName = null;
        await DropBackgroundAsync(AudioDefaults.StopFade);
    });

    // ---- General ----

    public Task SetMasterVolumeAsync(double volume) => GuardAsync(async () =>
    {
        _masterFade = null;
        _master = Math.Clamp(volume, 0, 1);
        foreach (var channel in _channels.Values.ToList())
            await _player.SetVolumeAsync(channel.Id, Effective(channel));
    });

    public Task PauseAllAsync() => GuardAsync(async () =>
    {
        if (Paused) return;
        await _player.PauseAllAsync();
        Freeze();
    });

    public Task ResumeAllAsync() => GuardAsync(ResumeIfPausedAsync);

    /// <summary>Para todo con el fundido corto; en pausa no hay fundido que corra, así que para en seco.</summary>
    public Task StopAllAsync() => GuardAsync(async () =>
    {
        if (Paused)
        {
            await StopNowAsync();
            return;
        }
        _sceneName = null;
        await DropBackgroundAsync(AudioDefaults.StopFade);
        foreach (var effect in _effects.ToList())
            await RemoveEffectAsync(effect, AudioDefaults.StopFade);
    });

    /// <summary>Tras renombrar una escena en el almacén: si es la que suena, la mezcla pasa a llamarla con el nombre nuevo.</summary>
    public Task RenameCurrentSceneAsync(string oldName, string newName) => GuardAsync(() =>
    {
        var matches = string.Equals(_sceneName, oldName, StringComparison.OrdinalIgnoreCase);
        if (matches) _sceneName = newName;
        return Task.FromResult(matches);
    });

    // Una sola toma del gate: ninguna orden encolada puede arrancar un canal entre la parada y el desecho.
    public async ValueTask DisposeAsync()
    {
        _host.RemoveBeforeSwitch(_beforeSwitch);
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _player.Ended -= OnEnded;
            _player.Failed -= OnFailed;
            _player.ExternalAction -= OnExternalAction;
            await StopNowAsync();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }
}
