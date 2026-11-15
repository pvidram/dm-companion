using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Resuelve los <c>![[imagen]]</c> de las notas contra la lista de imágenes del vault.
/// ResolvePath es síncrono (lo llama el renderizador): quien pinta una nota espera antes a EnsureLoadedAsync.
/// La lista se marca como obsoleta cuando FileChanged trae una imagen y se recarga en la siguiente EnsureLoadedAsync; mientras
/// tanto se sigue usando la anterior. Al cambiar de vault se descarta.
/// </summary>
public sealed class NoteImageResolver(VaultHost host)
{
    private readonly VaultFileUrls _urls = new(host);
    private readonly object _gate = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private VaultService? _vault;
    private Dictionary<string, DateTime> _files = new(StringComparer.OrdinalIgnoreCase);
    private bool _stale = true;

    /// <summary>
    /// Cambió una imagen del vault. Se lanza después de marcar la lista como obsoleta: quien espere a EnsureLoadedAsync al
    /// recibirlo ya ve la lista nueva. Puede llegar desde cualquier hilo.
    /// </summary>
    public event EventHandler? ImagesChanged;

    public async Task EnsureLoadedAsync()
    {
        await _loadGate.WaitAsync();
        try
        {
            await LoadAsync();
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task LoadAsync()
    {
        VaultService? vault;
        lock (_gate)
        {
            vault = host.Current;
            if (ReferenceEquals(vault, _vault) && !_stale) return;
            if (!ReferenceEquals(vault, _vault))
            {
                if (_vault is not null) _vault.FileChanged -= OnFileChanged;
                if (vault is not null) vault.FileChanged += OnFileChanged;
                _vault = vault;
                _files = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            }
            _stale = false;
        }
        if (vault is null) return;
        try
        {
            var listed = await vault.ListFilesAsync(MapPaths.IsImage);
            var files = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in listed) files[VaultPaths.Normalize(file.RelativePath)] = file.LastWriteTimeUtc;
            lock (_gate) if (ReferenceEquals(_vault, vault)) _files = files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (_gate) _stale = true;
        }
    }

    public string? ResolvePath(string target)
    {
        if (!VaultPaths.TryNormalizeSafe(VaultPaths.Normalize(target), out var path) || !MapPaths.IsImage(path)) return null;
        lock (_gate)
        {
            if (!ReferenceEquals(host.Current, _vault)) return null;
            foreach (var candidate in _files.Keys)
                if (candidate.Equals(path, StringComparison.OrdinalIgnoreCase)) return candidate;
            var suffix = "/" + path;
            string? found = null;
            foreach (var candidate in _files.Keys)
            {
                if (!candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                if (found is not null) return null;
                found = candidate;
            }
            return found;
        }
    }

    /// <summary>Fecha de modificación de esa imagen (ruta ya resuelta) en la lista cargada del vault vigente; null si no está.</summary>
    public DateTime? LastWriteOf(string path)
    {
        lock (_gate)
            return ReferenceEquals(host.Current, _vault) && _files.TryGetValue(path, out var stamp) ? stamp : null;
    }

    public Func<string, string?> TabletUrls() => target =>
    {
        if (ResolvePath(target) is not { } path) return null;
        DateTime stamp;
        lock (_gate) stamp = _files.GetValueOrDefault(path);
        return _urls.UrlFor(path, stamp);
    };

    // Solo una imagen (o un reescaneo) cambia la lista: con Syncthing llegan muchos cambios de otros ficheros y relistar el
    // vault entero en cada uno sería caro en la tablet.
    private void OnFileChanged(object? sender, VaultChange change)
    {
        if (change.Kind != VaultChangeKind.Rescan && !MapPaths.IsImage(change.RelativePath)) return;
        lock (_gate) _stale = true;
        ImagesChanged?.Invoke(this, EventArgs.Empty);
    }
}
