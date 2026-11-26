using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using Microsoft.Extensions.Logging;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Mapa abierto: estado, guardado diferido y reacción a cambios externos del `.state.json`.
/// Promesa central: un movimiento de token o un trazo nunca se pierde (segundo plano, cambio de mapa o de vault).
/// </summary>
public sealed partial class MapSession : IAsyncDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(750);
    public static readonly TimeSpan ReloadRetryDelay = TimeSpan.FromMilliseconds(250);
    public const int ReloadRetries = 3;

    private readonly VaultHost _host;
    private readonly IAppPreferences _prefs;
    private readonly TimeProvider _time;
    private readonly ILogger<MapSession> _logger;

    // Serializa escrituras y recargas: cada guardado escribe el State vigente en ese momento.
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    // Protege editor, revisiones y flags, que tocan la UI y los hilos del watcher.
    private readonly Lock _lock = new();
    private ITimer? _timer;
    private VaultService? _vault;
    private int _revision;
    private int _savedRevision;
    private DateTime? _expectedWrite;
    // Contenido que corresponde a _expectedWrite (lo último escrito o leído). El mtime solo no basta: en el almacenamiento
    // de Android el stat posterior puede no coincidir con el de la escritura, y Syncthing puede tocar el mtime sin cambiar
    // el contenido. Un mtime distinto con este mismo contenido no es un cambio externo.
    private string? _knownContent;
    private bool _overwrite;
    private bool _failureNotice;
    private bool _disposed;
    // Cada evento del watcher abre una época; abrir, cerrar o desechar la sesión también. Un reintento de recarga
    // solo sigue vivo mientras su época sea la vigente: así no pisa una recarga posterior ni actúa sobre otro mapa.
    private int _reloadEpoch;
    private readonly CancellationTokenSource _lifetime = new();
    // Se marca con el gate tomado y se notifica tras soltarlo: un suscriptor nunca corre con el gate ocupado.
    private int _changedPending;

    public MapSession(VaultHost host, IAppPreferences prefs, TimeProvider time, ILogger<MapSession> logger)
    {
        (_host, _prefs, _time, _logger) = (host, prefs, time, logger);
        // Se registra aquí, y no al revés, para evitar la dependencia circular VaultHost <-> MapSession.
        host.AddBeforeSwitch(FlushAndCloseAsync);
    }

    public MapDefinition? Current { get; private set; }
    public MapEditor? Editor { get; private set; }
    public bool IsReadOnly { get; private set; }
    public string? Notice { get; private set; }
    public string? ImageNotice { get; private set; }

    /// <summary>El aviso actual es un fallo de guardado: la UI ofrece descartar los cambios como vía de escape.</summary>
    public bool HasSaveFailure => _failureNotice;

    /// <summary>
    /// Hay cambios sin guardar o se están escribiendo: la UI muestra el indicador de guardado. Pasa a false cuando el
    /// guardado termina bien (y sigue en true si falla); cada cambio de valor se notifica con <see cref="Changed"/>.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get { lock (_lock) return Dirty && !IsReadOnly; }
    }

    public event EventHandler? Changed;

    private bool Dirty => _revision != _savedRevision;

    internal static string NewId() => Guid.NewGuid().ToString("N")[..12];


    // No se llama a ITimer.Change con el lock tomado: un ITimer reentrante (como el del hook de las pruebas) bloqueaba Close con una
    // recarga en vuelo. Un dispose concurrente que desecha el temporizador antes del Change lo absorben TryCloseAsync y FlushAsync.
    private void StopTimer()
    {
        ITimer? timer;
        lock (_lock) timer = _timer;
        timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private bool IsDisposed
    {
        get { lock (_lock) return _disposed; }
    }

    private void MarkChanged() => Interlocked.Exchange(ref _changedPending, 1);

    private void RaiseIfPending()
    {
        if (Interlocked.Exchange(ref _changedPending, 0) == 1) RaiseChanged();
    }

    // Un suscriptor que lanza no debe romper la sesión ni los demás suscriptores. Solo el tipo: el mensaje puede citar contenido.
    private void RaiseChanged()
    {
        var handlers = Changed?.GetInvocationList();
        if (handlers is null) return;
        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Un suscriptor de MapSession.Changed ha lanzado una excepción ({ExceptionType}).", ex.GetType().FullName);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Idempotente: solo la primera llamada guarda y libera; el host de escritorio puede desechar dos veces.
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        // Primero se desengancha el watcher y se cancelan los reintentos: una recarga nueva no puede colarse tras el guardado final.
        lock (_lock) Detach();
        await _lifetime.CancelAsync();
        await FlushCoreAsync();
        _timer?.Dispose();
        _lifetime.Dispose();
        _ioGate.Dispose();
    }
}
