using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class FileSystemVaultWatcher(string rootPath) : IVaultChangeSource
{
    private readonly string _root = Path.GetFullPath(rootPath);
    private FileSystemWatcher? _watcher;

    public event EventHandler<VaultChange>? Changed;

    public void Start()
    {
        if (_watcher is not null) return;

        _watcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Created += (_, e) => OnCreated(e.FullPath);
        _watcher.Changed += (_, e) => RaiseFile(e.FullPath, VaultChangeKind.Upserted);
        _watcher.Deleted += (_, e) => OnDeleted(e.FullPath);
        _watcher.Renamed += OnRenamed;
        _watcher.Error += (_, _) => RaiseRescan();
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    // inotify: los ficheros escritos en una carpeta nueva antes de que se registre su vigilancia
    // no generan evento, así que al aparecer una carpeta se emiten sus notas ya existentes.
    private void OnCreated(string fullPath)
    {
        if (!Directory.Exists(fullPath))
        {
            RaiseFile(fullPath, VaultChangeKind.Upserted);
            return;
        }
        try
        {
            foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
                RaiseFile(file, VaultChangeKind.Upserted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RaiseRescan();
        }
    }

    // Al borrar una carpeta (o moverla fuera) no llegan eventos de sus notas: se reescanea.
    private void OnDeleted(string fullPath)
    {
        var relativePath = VaultPaths.Normalize(Path.GetRelativePath(_root, fullPath));
        if (!IsTracked(relativePath) && !VaultPaths.IsExcluded(relativePath))
        {
            RaiseRescan();
            return;
        }
        RaiseFile(fullPath, VaultChangeKind.Deleted);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (Directory.Exists(e.FullPath))
        {
            RaiseRescan();
            return;
        }
        RaiseFile(e.OldFullPath, VaultChangeKind.Deleted);
        RaiseFile(e.FullPath, VaultChangeKind.Upserted);
    }

    private void RaiseFile(string fullPath, VaultChangeKind kind)
    {
        var relativePath = VaultPaths.Normalize(Path.GetRelativePath(_root, fullPath));
        if (!IsTracked(relativePath) || VaultPaths.IsExcluded(relativePath)) return;
        Changed?.Invoke(this, new VaultChange(relativePath, kind));
    }

    // Notas, estado de mapa e imágenes; cualquier otra cosa se ignora.
    private static bool IsTracked(string relativePath) =>
        VaultPaths.IsMarkdown(relativePath) || MapPaths.IsStateFile(VaultPaths.GetConflictOriginal(relativePath)) || MapPaths.IsImage(relativePath);

    private void RaiseRescan() => Changed?.Invoke(this, new VaultChange(string.Empty, VaultChangeKind.Rescan));
}
