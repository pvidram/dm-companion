using System.Globalization;
using DmCompanion.Core.Manuals;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Manuales PDF de <c>manuales/&lt;sistema activo&gt;/</c> del vault abierto. Se relistan al abrir el vault,
/// al cambiar el sistema activo y ante un evento de fichero sobre un .pdf de <c>manuales/</c>.
/// <see cref="Changed"/> se emite en un hilo del pool.
/// </summary>
public sealed class ManualLibrary : IDisposable
{
    private static readonly StringComparer TitleOrder = StringComparer.Create(new CultureInfo("es-ES"), ignoreCase: true);

    private readonly VaultHost _host;
    private readonly ILogger<ManualLibrary> _logger;
    private readonly object _gate = new();
    // No se libera en Dispose: un relistado en curso aún puede soltarlo, y sin AvailableWaitHandle no retiene recursos.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private VaultService? _vault;
    private string? _systemKey;
    private volatile bool _disposed;
    private volatile IReadOnlyList<ManualInfo> _manuals = [];
    private volatile string? _systemId;

    public ManualLibrary(VaultHost host, ILogger<ManualLibrary> logger)
    {
        _host = host;
        _logger = logger;
        _host.Changed += OnHostChanged;
        Attach();
    }

    public IReadOnlyList<ManualInfo> Manuals => _manuals;

    public string? SystemId => _systemId;

    public event EventHandler? Changed;

    public void Dispose()
    {
        _host.Changed -= OnHostChanged;
        _disposed = true;
        lock (_gate)
        {
            if (_vault is { } vault) vault.FileChanged -= OnFileChanged;
            _vault = null;
        }
    }

    /// <summary>
    /// Relista los manuales del sistema activo y avisa a los suscriptores. Los relistados van de uno en uno y cada uno publica
    /// su resultado, así que el último publica el más nuevo. Si falla el listado se conserva la lista anterior.
    /// </summary>
    public async Task RefreshAsync()
    {
        bool published;
        await _refreshLock.WaitAsync();
        try
        {
            published = await RefreshLockedAsync();
        }
        finally
        {
            _refreshLock.Release();
        }
        if (published) RaiseChanged();
    }

    private async Task<bool> RefreshLockedAsync()
    {
        if (_disposed) return false;
        var vault = _host.Current;
        var systemId = vault?.ActiveSystem.Id;
        IReadOnlyList<ManualInfo> manuals = [];
        if (vault is not null && systemId is not null)
        {
            try
            {
                var files = await vault.ListFilesAsync(path => ManualPaths.IsManualOf(path, systemId));
                manuals = [.. files
                    .Select(file => VaultPaths.Normalize(file.RelativePath))
                    .Select(path => new ManualInfo(path, ManualPaths.TitleOf(path), ManualPaths.GroupOf(path, systemId)))
                    .OrderBy(manual => manual.Group, TitleOrder)
                    .ThenBy(manual => manual.Title, TitleOrder)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("No se pudo listar manuales/ ({ExceptionType})", ex.GetType().Name);
                return false;
            }
        }
        // Si mientras tanto cambió el vault o el sistema, el relistado que ya está en cola publicará el bueno.
        var current = _host.Current;
        if (_disposed || !ReferenceEquals(current, vault) || current?.ActiveSystem.Id != systemId) return false;
        _manuals = manuals;
        _systemId = systemId;
        return true;
    }

    private void OnHostChanged(object? sender, EventArgs e) => Attach();

    private void Attach()
    {
        var vault = _host.Current;
        var key = vault?.ActiveSystem.Id;
        bool refresh;
        lock (_gate)
        {
            var sameVault = ReferenceEquals(vault, _vault);
            if (!sameVault)
            {
                if (_vault is { } previous) previous.FileChanged -= OnFileChanged;
                _vault = vault;
                if (vault is not null) vault.FileChanged += OnFileChanged;
            }
            // Host.Changed salta también con cada nota guardada: solo se relista si cambia el vault o el sistema.
            refresh = !sameVault || key != _systemKey;
            _systemKey = key;
        }
        if (refresh) _ = RefreshSafeAsync();
    }

    private void OnFileChanged(object? sender, VaultChange change)
    {
        if (change.Kind == VaultChangeKind.Rescan || IsManualPdf(VaultPaths.Normalize(change.RelativePath)))
            _ = RefreshSafeAsync();
    }

    private static bool IsManualPdf(string path) =>
        path.StartsWith(ManualPaths.Root + "/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshSafeAsync()
    {
        try
        {
            await Task.Run(RefreshAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo inesperado al relistar los manuales ({ExceptionType})", ex.GetType().Name);
        }
    }

    private void RaiseChanged()
    {
        var handler = Changed;
        if (handler is null) return;
        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Un suscriptor de ManualLibrary.Changed falló ({ExceptionType})", ex.GetType().Name);
            }
        }
    }
}
