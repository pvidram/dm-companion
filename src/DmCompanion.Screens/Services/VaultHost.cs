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
        try
        {
            if (skipIfCurrent && Current is { } open && SamePath(open.RootPath, path)) return true;
            // Si Create o LoadAsync fallan, el nuevo servicio ya se ha limpiado solo (LoadAsync libera el watcher).
            var service = factory.Create(path);
            await service.LoadAsync();
            // Primero se asigna el nuevo y luego se libera el anterior (best-effort): un Dispose que
            // lance nunca deja Current a medias ni deja huérfano el watcher del nuevo.
            var previous = Current;
            service.Changed += OnServiceChanged;
            Current = service;
            LastError = null;
            preferences.Set(PathKey, path);
            await ReleaseAsync(previous);
            return true;
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
            await CloseAsync();
            preferences.Remove(PathKey);
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
            logger.LogWarning(ex, "Fallo al liberar el vault anterior; se ignora.");
        }
    }

    private void OnServiceChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
