using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Mapa abierto: estado, guardado diferido y reacción a cambios externos del `.state.json`.
/// Promesa central: un movimiento de token o un trazo nunca se pierde (segundo plano, cambio de mapa o de vault).
/// </summary>
public sealed class MapSession : IAsyncDisposable
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

    public event EventHandler? Changed;

    private bool Dirty => _revision != _savedRevision;

    private static string NewId() => Guid.NewGuid().ToString("N")[..12];

    private string LastKey(VaultService vault) => "maps.last:" + vault.RootPath;

    public async Task OpenAsync(string mapNotePath)
    {
        if (_host.Current is not { } vault) return;
        var definition = vault.Index.Get(mapNotePath) is { } entity ? MapDefinition.FromEntity(entity) : null;
        if (definition is null)
        {
            await CloseAsync();
            return;
        }

        StopTimer();
        await _ioGate.WaitAsync();
        try
        {
            // Se guarda y se carga con el gate tomado; si entra un Mutate durante la carga, se vuelve a guardar.
            // Si no se puede guardar, el mapa actual se queda abierto con su estado y su aviso.
            for (;;)
            {
                if (!await SaveLockedAsync()) return;
                var (state, readOnly, notice, mtime) = await LoadStateAsync(vault, definition.StatePath);
                lock (_lock)
                {
                    if (Dirty && !IsReadOnly) continue;
                    Detach();
                    _vault = vault;
                    Current = definition;
                    Editor = new MapEditor(state, definition.Width, definition.Height, NewId);
                    IsReadOnly = readOnly;
                    Notice = notice;
                    ImageNotice = null;
                    _expectedWrite = mtime;
                    _revision = _savedRevision = 0;
                    _overwrite = _failureNotice = false;
                    vault.FileChanged += OnFileChanged;
                }
                break;
            }
            _prefs.Set(LastKey(vault), definition.NotePath);
            MarkChanged();
        }
        finally
        {
            _ioGate.Release();
            RaiseIfPending();
        }
    }

    public async Task OpenLastAsync()
    {
        if (_host.Current is not { } vault || _prefs.Get(LastKey(vault)) is not { } last) return;
        await OpenAsync(last);
    }

    /// <summary>Un mapa renombrado sin abrir sigue siendo el último recordado: la preferencia pasa a la ruta nueva.</summary>
    public void RememberRenamed(string oldNotePath, string newNotePath)
    {
        if (_host.Current is { } vault && _prefs.Get(LastKey(vault)) == oldNotePath) _prefs.Set(LastKey(vault), newNotePath);
    }

    /// <summary>Un mapa borrado deja de ser el último recordado.</summary>
    public void ForgetDeleted(string notePath)
    {
        if (_host.Current is { } vault && _prefs.Get(LastKey(vault)) == notePath) _prefs.Remove(LastKey(vault));
    }

    public async Task CloseAsync() => await TryCloseAsync();

    /// <summary>
    /// Vía de escape de un guardado que falla de forma permanente: tira los cambios en memoria sin escribir y cierra el mapa.
    /// Es la única forma de soltar el mapa (y poder cambiar de vault) sin guardar.
    /// </summary>
    public async Task DiscardAndCloseAsync()
    {
        StopTimer();
        await _ioGate.WaitAsync();
        try
        {
            lock (_lock)
            {
                Detach();
                (Current, Editor, _vault, Notice, ImageNotice, IsReadOnly) = (null, null, null, null, null, false);
                _revision = _savedRevision = 0;
                _expectedWrite = null;
                _overwrite = _failureNotice = false;
            }
            MarkChanged();
        }
        finally
        {
            _ioGate.Release();
            RaiseIfPending();
        }
    }

    // false si quedan cambios sin guardar: entonces el mapa sigue abierto.
    private async Task<bool> TryCloseAsync()
    {
        // Desechada: el guardado final ya se hizo y el gate no existe; cerrar de nuevo no debe abortar el cambio de vault.
        if (IsDisposed) return true;
        StopTimer();
        try
        {
            await _ioGate.WaitAsync();
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            // El dispose terminó entre la comprobación y el gate: su guardado final ya se hizo.
            return true;
        }
        try
        {
            for (;;)
            {
                if (!await SaveLockedAsync()) return false;
                lock (_lock)
                {
                    if (Dirty && !IsReadOnly) continue;
                    Detach();
                    (Current, Editor, _vault, Notice, ImageNotice, IsReadOnly) = (null, null, null, null, null, false);
                    _revision = _savedRevision = 0;
                }
                break;
            }
            MarkChanged();
        }
        finally
        {
            _ioGate.Release();
            RaiseIfPending();
        }
        return true;
    }

    // Falla cerrado: cualquier fallo inesperado cuenta como guardado fallido, para que el cambio de vault se aborte.
    private async Task FlushAndCloseAsync()
    {
        bool closed;
        try
        {
            closed = await TryCloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo inesperado al cerrar el mapa ({ExceptionType}).", ex.GetType().FullName);
            throw new MapSaveFailedException("No se pudo guardar el mapa abierto.");
        }
        if (!closed) throw new MapSaveFailedException(Notice ?? "No se pudo guardar el mapa abierto.");
    }

    private void StopTimer()
    {
        lock (_lock) _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Mutate(Func<MapEditor, bool> change)
    {
        lock (_lock)
        {
            if (_disposed || IsReadOnly || Editor is not { } editor || !change(editor)) return;
            _revision++;
            ScheduleSave();
        }
        RaiseChanged();
    }

    // Un onload tardío del mapa A no debe aplicarse al B: se exige la nota con la que se cargó la imagen.
    public void SetImageSize(string notePath, double width, double height)
    {
        lock (_lock)
        {
            if (Editor is not { } editor || Current is not { } current || current.NotePath != notePath) return;
            if (current.Width == width && current.Height == height) return;
            Current = current with { Width = width, Height = height };
            Editor = new MapEditor(editor.State, width, height, NewId);
        }
        RaiseChanged();
    }

    /// <summary>El lienzo no pudo cargar la imagen del mapa. Aviso aparte de <see cref="Notice"/>: ninguno pisa al otro.</summary>
    public void ReportImageMissing()
    {
        lock (_lock) ImageNotice = "No se encuentra la imagen del mapa";
        RaiseChanged();
    }

    /// <summary>No hace nada una vez desechada la sesión: el gate ya no existe y un guardado tardío no debe escribir.</summary>
    public async Task FlushAsync()
    {
        if (IsDisposed) return;
        try
        {
            await FlushCoreAsync();
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            // Se desechó entre la comprobación y el gate: no queda nada que guardar.
        }
    }

    private bool IsDisposed
    {
        get { lock (_lock) return _disposed; }
    }

    private async Task FlushCoreAsync()
    {
        StopTimer();
        await _ioGate.WaitAsync();
        try
        {
            await SaveLockedAsync();
        }
        finally
        {
            _ioGate.Release();
            RaiseIfPending();
        }
    }

    // Debe llamarse dentro de _lock.
    private void ScheduleSave()
    {
        if (_disposed) return;
        if (_timer is null) _timer = _time.CreateTimer(_ => _ = SaveObservedAsync(), null, SaveDelay, Timeout.InfiniteTimeSpan);
        else _timer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    // Fire-and-forget observado: IOException y permisos ya se convierten en Notice dentro de FlushAsync.
    private async Task SaveObservedAsync()
    {
        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo inesperado al guardar el mapa ({ExceptionType}).", ex.GetType().FullName);
        }
    }

    // Con _ioGate tomado.
    // true si no quedan cambios pendientes por este guardado; false si falló y siguen en memoria.
    private async Task<bool> SaveLockedAsync()
    {
        try
        {
            string json;
            int revision;
            VaultService vault;
            MapDefinition definition;
            lock (_lock)
            {
                if (!Dirty || IsReadOnly || Editor is not { } editor || _vault is not { } v || Current is not { } c) return true;
                (json, revision, vault, definition) = (MapStateSerializer.Write(editor.State), _revision, v, c);
            }

            var result = await WriteAsync(vault, definition.StatePath, json);
            if (result.Status != WriteStatus.Written)
            {
                // Cambió en disco sin que lo hubiéramos visto: gana lo local, avisando.
                result = await vault.WriteAsync(definition.StatePath, json, WritePrecondition.Overwrite);
                SetNotice("El mapa había cambiado en disco; se ha sobrescrito con tus cambios.", failure: false);
            }
            lock (_lock)
            {
                _expectedWrite = result.LastWriteTimeUtc;
                _savedRevision = revision;
                _overwrite = false;
                if (_failureNotice) { Notice = null; _failureNotice = false; }
            }
            MarkChanged();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sigue sucio: el siguiente Mutate (o Flush) lo reintenta.
            _logger.LogWarning("No se pudo guardar el mapa ({ExceptionType}).", ex.GetType().FullName);
            SetNotice($"No se pudo guardar el mapa: {ex.Message}", failure: true);
            return false;
        }
        catch (Exception ex)
        {
            // Falla cerrado: un error inesperado también es un guardado fallido (aviso y mapa sucio), nunca un éxito.
            _logger.LogError("Fallo inesperado al guardar el mapa ({ExceptionType}).", ex.GetType().FullName);
            SetNotice("No se pudo guardar el mapa por un error inesperado.", failure: true);
            return false;
        }
    }

    private Task<WriteResult> WriteAsync(VaultService vault, string path, string json)
    {
        WritePrecondition precondition;
        lock (_lock)
        {
            precondition = _overwrite ? WritePrecondition.Overwrite
                : _expectedWrite is { } mtime ? WritePrecondition.UnchangedSince(mtime)
                : WritePrecondition.MustNotExist;
        }
        return vault.WriteAsync(path, json, precondition);
    }

    private void SetNotice(string notice, bool failure)
    {
        lock (_lock) (Notice, _failureNotice) = (notice, failure);
        MarkChanged();
    }

    private async Task<(MapState State, bool ReadOnly, string? Notice, DateTime? Mtime)> LoadStateAsync(VaultService vault, string path)
    {
        var load = await ReadStateAsync(vault, path);
        return (load.State, load.ReadOnly, load.Notice, load.Mtime);
    }

    // Transient: la lectura falló o el contenido no es válido; en una recarga externa puede ser un escritor a medias.
    // Una versión de formato no soportada no es transitoria: no mejora esperando.
    private sealed record StateLoad(MapState State, bool ReadOnly, string? Notice, DateTime? Mtime, bool Transient = false, bool Missing = false);

    private async Task<StateLoad> ReadStateAsync(VaultService vault, string path)
    {
        try
        {
            if (await vault.StatAsync(path) is null) return new StateLoad(MapState.Empty, false, null, null, Missing: true);
            var file = await vault.ReadRawAsync(path);
            var read = MapStateSerializer.Read(file.Content);
            return read.Status switch
            {
                MapStateReadStatus.Ok => new StateLoad(read.State, false, null, file.LastWriteTimeUtc),
                MapStateReadStatus.FutureVersion => new StateLoad(read.State, true,
                    "El mapa se guardó con una versión más nueva de la app: se abre en solo lectura.", file.LastWriteTimeUtc),
                _ => new StateLoad(MapState.Empty, true,
                    "El fichero de estado del mapa no es válido: se abre en solo lectura y no se modificará.", file.LastWriteTimeUtc, Transient: true),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new StateLoad(MapState.Empty, true, $"No se pudo leer el estado del mapa: {ex.Message}", null, Transient: true);
        }
    }

    private void OnFileChanged(object? sender, VaultChange change)
    {
        if (Current is not { } current || !string.Equals(VaultPaths.Normalize(change.RelativePath), current.StatePath, StringComparison.OrdinalIgnoreCase))
            return;
        var epoch = Interlocked.Increment(ref _reloadEpoch);
        _ = ReloadObservedAsync(epoch);
    }

    // Una lectura vacía o inválida no se aplica al momento: puede ser un escritor no atómico a medias. El gate se suelta
    // durante la espera, y cada intento vuelve a comprobarlo todo (mtime, cambios locales, mapa abierto).
    private async Task ReloadObservedAsync(int epoch)
    {
        try
        {
            for (var attempt = 0; attempt <= ReloadRetries; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(ReloadRetryDelay, _time, _lifetime.Token);
                    if (Volatile.Read(ref _reloadEpoch) != epoch) return;
                }
                if (!await ReloadAsync(epoch, lastAttempt: attempt == ReloadRetries)) return;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // La sesión se ha desechado: los reintentos pendientes se abandonan.
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            // El gate se liberó con esta recarga en espera: la sesión ya no existe.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("No se pudo recargar el mapa tras un cambio externo ({ExceptionType}).", ex.GetType().FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo inesperado al recargar el mapa ({ExceptionType}).", ex.GetType().FullName);
        }
    }

    // true si la lectura fue inválida y conviene reintentarla.
    private async Task<bool> ReloadAsync(int epoch, bool lastAttempt)
    {
        if (IsDisposed) return false;
        await _ioGate.WaitAsync();
        try
        {
            if (IsDisposed || Volatile.Read(ref _reloadEpoch) != epoch) return false;
            if (_vault is not { } vault || Current is not { } current) return false;
            var info = await vault.StatAsync(current.StatePath);
            // Escritura propia (o ráfaga repetida del watcher): mismo mtime, nada que hacer.
            if (info?.LastWriteTimeUtc == _expectedWrite) return false;
            if (info is null)
            {
                lock (_lock) _expectedWrite = null;
                return false;
            }

            // La comprobación de Dirty y la foto de la revisión van en el mismo lock: un Mutate no puede colarse entre ambas.
            bool dirty;
            int revision;
            lock (_lock)
            {
                dirty = Dirty;
                revision = _revision;
                if (dirty) _overwrite = true;
            }
            if (dirty)
            {
                SetNotice("El mapa ha cambiado en disco; el próximo guardado lo sobrescribirá con tus cambios.", failure: false);
                return false;
            }
            var load = await ReadStateAsync(vault, current.StatePath);
            // Borrado entre los dos stat: como si no existiera, se conserva el estado.
            if (load.Missing)
            {
                lock (_lock) _expectedWrite = null;
                return false;
            }
            // Se conserva el estado anterior sin tocar IsReadOnly ni _expectedWrite: el siguiente intento vuelve a leer.
            if (load.Transient && !lastAttempt) return true;
            // Un resultado final inválido no recuerda el mtime: la escritura completa posterior con el mismo mtime se relee.
            var (state, readOnly, notice) = (load.State, load.ReadOnly, load.Notice);
            var mtime = load.Transient ? null : load.Mtime;
            bool mutatedMeanwhile;
            lock (_lock)
            {
                mutatedMeanwhile = _revision != revision;
                if (mutatedMeanwhile) _overwrite = true;
                else
                {
                    Editor = new MapEditor(state, current.Width, current.Height, NewId);
                    (IsReadOnly, Notice, _expectedWrite, _failureNotice) = (readOnly, notice, mtime, false);
                }
            }
            if (mutatedMeanwhile)
            {
                SetNotice("El mapa ha cambiado en disco; el próximo guardado lo sobrescribirá con tus cambios.", failure: false);
                return false;
            }
            MarkChanged();
            return false;
        }
        finally
        {
            _ioGate.Release();
            RaiseIfPending();
        }
    }

    private void Detach()
    {
        Interlocked.Increment(ref _reloadEpoch);
        if (_vault is { } vault) vault.FileChanged -= OnFileChanged;
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
