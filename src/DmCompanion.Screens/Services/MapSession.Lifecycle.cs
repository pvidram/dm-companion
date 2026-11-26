using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Abrir, cerrar y descartar el mapa; el cambio de vault lo cierra guardando antes.
public sealed partial class MapSession
{
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
                var (state, readOnly, notice, mtime, content) = await LoadStateAsync(vault, definition.StatePath);
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
                    (_expectedWrite, _knownContent) = (mtime, content);
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
                (_expectedWrite, _knownContent) = (null, null);
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
        try
        {
            return await TryCloseCoreAsync();
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            // El dispose avanzó en paralelo tras la comprobación: desechó el temporizador antes de pararlo, el gate antes de
            // tomarlo o el gate mientras este cierre lo tenía. Su guardado final ya se hizo.
            return true;
        }
    }

    private async Task<bool> TryCloseCoreAsync()
    {
        StopTimer();
        await _ioGate.WaitAsync();
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
}
