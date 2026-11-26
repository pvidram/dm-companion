using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Reacción a cambios externos del `.state.json` (watcher del vault).
public sealed partial class MapSession
{
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
            if (info?.LastWriteTimeUtc == _expectedWrite)
            {
                _logger.LogDebug("Evento del watcher sobre el estado del mapa: mtime coincide.");
                return false;
            }
            if (info is null)
            {
                lock (_lock) (_expectedWrite, _knownContent) = (null, null);
                return false;
            }

            var load = await ReadStateAsync(vault, current.StatePath);
            // Borrado entre los dos stat: como si no existiera, se conserva el estado.
            if (load.Missing)
            {
                lock (_lock) (_expectedWrite, _knownContent) = (null, null);
                return false;
            }
            if (load.Content is { } content && content == _knownContent)
            {
                _logger.LogInformation("Evento del watcher sobre el estado del mapa: mtime distinto ({DeltaMs} ms) con el mismo contenido; no es un cambio externo.",
                    _expectedWrite is { } expected ? (info.LastWriteTimeUtc - expected).TotalMilliseconds : null);
                lock (_lock) _expectedWrite = load.Mtime;
                return false;
            }

            // La comprobación de Dirty y la aplicación van en el mismo lock: un Mutate durante la lectura cuenta como cambio local.
            // Un resultado final inválido no recuerda el mtime: la escritura completa posterior con el mismo mtime se relee.
            bool dirty;
            var retry = false;
            lock (_lock)
            {
                dirty = Dirty;
                if (dirty) _overwrite = true;
                // Se conserva el estado anterior sin tocar IsReadOnly ni _expectedWrite: el siguiente intento vuelve a leer.
                else if (load.Transient && !lastAttempt) retry = true;
                else
                {
                    Editor = new MapEditor(load.State, current.Width, current.Height, NewId);
                    (IsReadOnly, Notice, _failureNotice) = (load.ReadOnly, load.Notice, false);
                    (_expectedWrite, _knownContent) = load.Transient ? (null, null) : (load.Mtime, load.Content);
                }
            }
            // Con cambios locales pendientes gana lo local: el próximo guardado sobrescribe, sin aviso.
            // Un intento que se reintentará no se registra: un escritor a medias daría una línea por intento.
            if (!retry) _logger.LogInformation("Evento del watcher sobre el estado del mapa: cambio externo (cambios locales pendientes: {Dirty}).", dirty);
            if (!dirty && !retry) MarkChanged();
            return retry;
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
}
