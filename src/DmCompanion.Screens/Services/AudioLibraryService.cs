using DmCompanion.Core.Audio;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Biblioteca de <c>_audio/</c> del vault abierto; se reconstruye al cambiar de vault y ante cualquier
/// evento de fichero bajo <c>_audio/</c>. <see cref="Changed"/> se emite en un hilo del pool.
/// </summary>
public sealed class AudioLibraryService : IDisposable
{
    private readonly VaultHost _host;
    private readonly ILogger<AudioLibraryService> _logger;
    private readonly object _gate = new();
    private VaultService? _vault;
    private int _generation;
    private bool _running;
    private bool _pending;
    private volatile AudioLibrary _current = AudioLibrary.Empty;

    public AudioLibraryService(VaultHost host, ILogger<AudioLibraryService> logger)
    {
        _host = host;
        _logger = logger;
        _host.Changed += OnHostChanged;
        Attach();
    }

    public AudioLibrary Current => _current;

    public event EventHandler? Changed;

    public void Dispose()
    {
        _host.Changed -= OnHostChanged;
        lock (_gate)
        {
            _generation++;
            _pending = false;
            if (_vault is { } vault) vault.FileChanged -= OnFileChanged;
            _vault = null;
        }
    }

    private void OnHostChanged(object? sender, EventArgs e) => Attach();

    private void Attach()
    {
        var vault = _host.Current;
        lock (_gate)
        {
            if (ReferenceEquals(vault, _vault)) return;
            if (_vault is { } previous) previous.FileChanged -= OnFileChanged;
            _vault = vault;
            if (vault is not null) vault.FileChanged += OnFileChanged;
        }
        if (vault is null) Publish(AudioLibrary.Empty, Interlocked.Increment(ref _generation));
        else RequestRebuild();
    }

    private void OnFileChanged(object? sender, VaultChange change)
    {
        if (change.Kind == VaultChangeKind.Rescan || AffectsLibrary(VaultPaths.Normalize(change.RelativePath))) RequestRebuild();
    }

    // Lo que la app guarda en _audio/_dm/ (escenas, favoritos) no son pistas: no hace falta releer la biblioteca.
    private static bool AffectsLibrary(string path) =>
        path.StartsWith(AudioPaths.Root + "/", StringComparison.OrdinalIgnoreCase)
        && !path.Equals(AudioPaths.AppFolder, StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith(AudioPaths.AppFolder + "/", StringComparison.OrdinalIgnoreCase);

    // Como mucho una reconstrucción en vuelo y una pendiente: las peticiones intermedias se funden en una.
    private void RequestRebuild()
    {
        lock (_gate)
        {
            _pending = true;
            if (_running) return;
            _running = true;
        }
        _ = Task.Run(RebuildLoopAsync);
    }

    private async Task RebuildLoopAsync()
    {
        try
        {
            while (true)
            {
                VaultService? vault;
                lock (_gate)
                {
                    if (!_pending)
                    {
                        _running = false;
                        return;
                    }
                    _pending = false;
                    vault = _vault;
                }
                if (vault is not null) await RebuildOnceAsync(vault);
            }
        }
        catch (Exception ex)
        {
            lock (_gate) _running = false;
            _logger.LogError("Fallo inesperado al reconstruir la biblioteca de audio ({ExceptionType})", ex.GetType().Name);
        }
    }

    private async Task RebuildOnceAsync(VaultService vault)
    {
        var generation = Interlocked.Increment(ref _generation);
        try
        {
            var files = await vault.ListFilesAsync(AudioPaths.IsInLibrary);
            var library = AudioLibrary.Build(files.Select(file => file.RelativePath));
            lock (_gate)
            {
                if (!ReferenceEquals(vault, _vault)) return;
            }
            Publish(library, generation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo listar la biblioteca de audio ({ExceptionType})", ex.GetType().Name);
        }
    }

    // Una reconstrucción más nueva descarta a la anterior para no pisar con datos viejos.
    private void Publish(AudioLibrary library, int generation)
    {
        lock (_gate)
        {
            if (generation != _generation) return;
            _current = library;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
