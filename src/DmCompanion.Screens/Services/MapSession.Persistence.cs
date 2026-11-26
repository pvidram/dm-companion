using System.Globalization;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Guardado diferido y lectura del `.state.json`.
public sealed partial class MapSession
{
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
                // El fichero no coincide con lo esperado: gana lo local, sin aviso (el indicador de guardado basta). Con el mismo
                // contenido no hay nada que perder; con otro, se sobrescribe como siempre. Solo cambia lo que se registra.
                await LogUnexpectedDiskStateAsync(vault, definition.StatePath, result.Status);
                result = await vault.WriteAsync(definition.StatePath, json, WritePrecondition.Overwrite);
            }
            lock (_lock)
            {
                (_expectedWrite, _knownContent) = (result.LastWriteTimeUtc, json);
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

    // Con _ioGate tomado. Diagnóstico para `adb logcat`: distingue un mtime distinto con el mismo contenido (no es un cambio
    // externo) de un cambio real en disco. Sin rutas ni contenido; de una excepción, solo el tipo.
    private async Task LogUnexpectedDiskStateAsync(VaultService vault, string path, WriteStatus status)
    {
        DateTime? expected;
        string? known;
        lock (_lock) (expected, known) = (_expectedWrite, _knownContent);
        VaultFileContent disk;
        try
        {
            disk = await vault.ReadRawAsync(path);
        }
        // Cualquier fallo del diagnóstico se registra y el guardado sigue: un diagnóstico no puede impedir guardar.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Al guardar: el estado del mapa no coincidía con el esperado ({WriteStatus}) y no se pudo leer para compararlo ({ExceptionType}); se sobrescribe con lo local.",
                status, ex.GetType().FullName);
            return;
        }
        var delta = expected is { } reference
            ? string.Create(CultureInfo.InvariantCulture, $"{(disk.LastWriteTimeUtc - reference).TotalMilliseconds:0} ms")
            : "sin referencia";
        if (known is not null && disk.Content == known)
            _logger.LogDebug("Al guardar: mtime distinto ({Delta}) con el mismo contenido; no es un cambio externo.", delta);
        else
            _logger.LogWarning("Al guardar: el estado del mapa había cambiado en disco (mtime {Delta}); se sobrescribe con lo local.", delta);
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

    private async Task<(MapState State, bool ReadOnly, string? Notice, DateTime? Mtime, string? Content)> LoadStateAsync(VaultService vault, string path)
    {
        var load = await ReadStateAsync(vault, path);
        return (load.State, load.ReadOnly, load.Notice, load.Mtime, load.Content);
    }

    // Transient: la lectura falló o el contenido no es válido; en una recarga externa puede ser un escritor a medias.
    // Una versión de formato no soportada no es transitoria: no mejora esperando.
    // Content: el texto leído, solo si es un estado válido (aunque sea de una versión más nueva).
    private sealed record StateLoad(MapState State, bool ReadOnly, string? Notice, DateTime? Mtime, bool Transient = false, bool Missing = false, string? Content = null);

    private string? RepairNotice(int repairs)
    {
        if (repairs <= 0) return null;
        _logger.LogWarning("Se han corregido {Repairs} datos de combate incoherentes al leer el estado del mapa.", repairs);
        return $"Se han corregido {repairs} datos de combate incoherentes del mapa.";
    }

    private async Task<StateLoad> ReadStateAsync(VaultService vault, string path)
    {
        try
        {
            if (await vault.StatAsync(path) is null) return new StateLoad(MapState.Empty, false, null, null, Missing: true);
            var file = await vault.ReadRawAsync(path);
            var read = MapStateSerializer.Read(file.Content);
            return read.Status switch
            {
                MapStateReadStatus.Ok => new StateLoad(read.State, false, RepairNotice(read.Repairs), file.LastWriteTimeUtc, Content: file.Content),
                MapStateReadStatus.FutureVersion => new StateLoad(read.State, true,
                    "El mapa se guardó con una versión más nueva de la app: se abre en solo lectura.", file.LastWriteTimeUtc, Content: file.Content),
                _ => new StateLoad(MapState.Empty, true,
                    "El fichero de estado del mapa no es válido: se abre en solo lectura y no se modificará.", file.LastWriteTimeUtc, Transient: true),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new StateLoad(MapState.Empty, true, $"No se pudo leer el estado del mapa: {ex.Message}", null, Transient: true);
        }
    }
}
