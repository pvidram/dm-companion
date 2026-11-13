using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Hace de factoría: la carpeta del vault se elige en tiempo de ejecución, así que el
// VaultService no puede registrarse directamente en el contenedor de DI.
public sealed class VaultHost(IVaultServiceFactory factory, IAppPreferences preferences, ILogger<VaultHost> logger) : IAsyncDisposable
{
    private const string PathKey = "vault_path";

    public VaultService? Current { get; private set; }

    public string? LastError { get; private set; }

    public string? SavedPath => preferences.Get(PathKey);

    public event EventHandler? Changed;

    // Handlers que deben terminar antes de cambiar o cerrar el vault (p. ej. guardar el mapa abierto).
    private readonly List<Func<Task>> _beforeSwitch = [];

    public void AddBeforeSwitch(Func<Task> handler)
    {
        lock (_beforeSwitch) _beforeSwitch.Add(handler);
    }

    private async Task RunBeforeSwitchAsync()
    {
        Func<Task>[] handlers;
        lock (_beforeSwitch) handlers = [.. _beforeSwitch];
        foreach (var handler in handlers)
        {
            try
            {
                await handler();
            }
            catch (Exception ex) when (ex is not MapSaveFailedException)
            {
                // Falla cerrado: si un handler (p. ej. el guardado del mapa) falla, no se cambia de vault. Solo el tipo, sin mensaje.
                logger.LogError("Fallo un handler previo al cambio de vault ({ExceptionType}).", ex.GetType().FullName);
                throw new MapSaveFailedException("No se pudo completar el cierre previo al cambio de vault.");
            }
        }
    }

    // Serializa aperturas: dos toques seguidos no deben dejar un servicio huérfano.
    private readonly SemaphoreSlim _openGate = new(1, 1);

    // El nuevo servicio se carga antes de tocar el actual: si falla, el vault anterior sigue abierto.
    public Task<bool> OpenAsync(string path) => OpenCoreAsync(path, skipIfCurrent: false);

    // Como OpenAsync, pero tras esperar el semáforo no recarga si una apertura en curso ya dejó ese vault abierto.
    public Task<bool> OpenIfNotCurrentAsync(string path) => OpenCoreAsync(path, skipIfCurrent: true);

    private static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd('/', '\\'), b.TrimEnd('/', '\\'), StringComparison.Ordinal);

    private async Task<bool> OpenCoreAsync(string path, bool skipIfCurrent)
    {
        await _openGate.WaitAsync();
        VaultService? pending = null;
        try
        {
            if (skipIfCurrent && Current is { } open && SamePath(open.RootPath, path)) return true;
            // Si Create o LoadAsync fallan, el nuevo servicio ya se ha limpiado solo (LoadAsync libera el watcher).
            var service = factory.Create(path);
            pending = service;
            await service.LoadAsync();
            // Primero se asigna el nuevo y luego se libera el anterior (best-effort): un Dispose que
            // lance nunca deja Current a medias ni deja huérfano el watcher del nuevo.
            await RunBeforeSwitchAsync();
            var previous = Current;
            service.Changed += OnServiceChanged;
            Current = service;
            LastError = null;
            preferences.Set(PathKey, path);
            pending = null;
            await ReleaseAsync(previous);
            return true;
        }
        catch (MapSaveFailedException ex)
        {
            // Los cambios del mapa no se han podido guardar: el vault actual sigue abierto y el nuevo se libera.
            await ReleaseAsync(pending);
            LastError = $"No se cambia de vault: {ex.Message}";
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastError = $"No se puede abrir el vault en «{path}»: {ex.Message}";
            return false;
        }
        finally
        {
            _openGate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ForgetAsync()
    {
        await _openGate.WaitAsync();
        try
        {
            await RunBeforeSwitchAsync();
            await CloseAsync();
            preferences.Remove(PathKey);
        }
        catch (MapSaveFailedException ex)
        {
            LastError = $"No se olvida el vault: {ex.Message}";
        }
        finally
        {
            _openGate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _openGate.WaitAsync();
        try
        {
            try
            {
                await RunBeforeSwitchAsync();
            }
            catch (MapSaveFailedException ex)
            {
                // La app se cierra: no hay donde avisar; el fallo queda en el log.
                logger.LogError("No se pudo guardar el mapa al cerrar ({ExceptionType}).", ex.GetType().FullName);
            }
            await CloseAsync();
        }
        finally
        {
            _openGate.Release();
        }
    }

    private async Task CloseAsync()
    {
        var previous = Current;
        Current = null;
        await ReleaseAsync(previous);
    }

    // Best-effort: un fallo al liberar un servicio se registra, no se propaga.
    private async Task ReleaseAsync(VaultService? service)
    {
        if (service is null) return;
        service.Changed -= OnServiceChanged;
        try
        {
            await service.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Fallo al liberar el vault anterior; se ignora ({ExceptionType}).", ex.GetType().Name);
        }
    }

    private void OnServiceChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
