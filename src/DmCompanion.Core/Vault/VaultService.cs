using System.Collections.Concurrent;
using System.Threading.Channels;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Core.Vault;

public enum ConflictChoice { KeepOriginal, KeepConflictCopy }

public enum ConflictResolutionStatus { Resolved, OriginalChanged, OriginalMissing }

public sealed record ConflictResolution(ConflictResolutionStatus Status);

public sealed record CreateNoteResult(string RelativePath, WriteStatus Status);

public sealed class VaultService(IVaultStorage storage, IVaultChangeSource changes, ILogger<VaultService> logger) : IAsyncDisposable
{
    private const int MaxReadAttempts = 3;
    private readonly Channel<VaultChange> _queue = Channel.CreateUnbounded<VaultChange>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Lock _gate = new();
    private Task? _consumer;
    private int _state; // 0 = nuevo, 1 = cargado, 2 = liberado
    private VaultIndex _index = VaultIndex.Empty;
    private IReadOnlyList<SyncConflict> _conflicts = [];

    public event EventHandler? Changed;

    public string RootPath => storage.RootPath;

    public VaultIndex Index { get { lock (_gate) return _index; } }

    public IReadOnlyList<SyncConflict> Conflicts { get { lock (_gate) return _conflicts; } }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_state == 2, this);
            if (_state == 1) return;
            _state = 1;
        }

        // Suscribirse antes del escaneo: los cambios que ocurran durante él quedan en cola.
        try
        {
            changes.Changed += OnChange;
            changes.Start();
            await RebuildAsync(ct);
        }
        catch
        {
            // Sin carga no hay servicio a medias: se libera todo y la siguiente llamada falla con ObjectDisposedException.
            lock (_gate) _state = 2;
            changes.Changed -= OnChange;
            changes.Dispose();
            _queue.Writer.TryComplete();
            _shutdown.Dispose();
            throw;
        }
        _consumer = Task.Run(() => ConsumeAsync(_shutdown.Token), CancellationToken.None);
        RaiseChanged();
    }

    public Task<VaultFileContent> ReadRawAsync(string relativePath, CancellationToken ct = default) =>
        storage.ReadAsync(relativePath, ct);

    public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default) =>
        storage.ReadBytesAsync(relativePath, ct);

    public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default) =>
        storage.StatAsync(relativePath, ct);

    public async Task<WriteResult> SaveAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default)
    {
        var result = await storage.WriteAsync(relativePath, content, precondition, ct);
        if (result.Status != WriteStatus.Written) return result;

        if (VaultPaths.IsIndexable(relativePath))
        {
            var entity = VaultEntityFactory.Create(relativePath, content, result.LastWriteTimeUtc);
            lock (_gate) _index = _index.Upsert(entity);
        }
        RaiseChanged();
        return result;
    }

    public async Task<CreateNoteResult> CreateNoteAsync(EntityType type, string title, string? folder = null, CancellationToken ct = default)
    {
        var directory = VaultPaths.Normalize(folder ?? NoteTemplates.DefaultFolder(type)).TrimEnd('/');
        var fileName = NoteFileName.FromTitle(title) + ".md";
        var relativePath = directory.Length == 0 ? fileName : $"{directory}/{fileName}";
        var result = await SaveAsync(relativePath, NoteTemplates.Create(type, title), WritePrecondition.MustNotExist, ct);
        return new CreateNoteResult(relativePath, result.Status);
    }

    public async Task<ConflictResolution> ResolveConflictAsync(
        SyncConflict conflict, ConflictChoice choice, DateTime? originalLastWriteUtc, CancellationToken ct = default)
    {
        if (!VaultPaths.IsConflictFile(conflict.ConflictRelativePath))
            throw new ArgumentException($"«{conflict.ConflictRelativePath}» no es un fichero de conflicto de sincronización.", nameof(conflict));

        var status = choice == ConflictChoice.KeepConflictCopy
            ? await KeepCopyAsync(conflict, originalLastWriteUtc, ct)
            : await KeepOriginalAsync(conflict, originalLastWriteUtc, ct);

        await RefreshConflictsAsync(ct);
        RaiseChanged();
        return new ConflictResolution(status);
    }

    // Si el original no existía al mostrar el conflicto, la copia puede ser la única versión: no se borra.
    private async Task<ConflictResolutionStatus> KeepOriginalAsync(SyncConflict conflict, DateTime? originalLastWriteUtc, CancellationToken ct)
    {
        if (originalLastWriteUtc is null || !await OriginalExistsAsync(conflict, ct)) return ConflictResolutionStatus.OriginalMissing;
        await storage.DeleteAsync(conflict.ConflictRelativePath, ct);
        return ConflictResolutionStatus.Resolved;
    }

    private async Task<bool> OriginalExistsAsync(SyncConflict conflict, CancellationToken ct)
    {
        try
        {
            await storage.ReadAsync(conflict.OriginalRelativePath, ct);
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            logger.LogWarning(ex, "El original {Path} ya no existe; se conserva la copia en conflicto", conflict.OriginalRelativePath);
            return false;
        }
    }

    private async Task<ConflictResolutionStatus> KeepCopyAsync(SyncConflict conflict, DateTime? originalLastWriteUtc, CancellationToken ct)
    {
        var copy = await storage.ReadAsync(conflict.ConflictRelativePath, ct);
        var precondition = originalLastWriteUtc is { } expected ? WritePrecondition.UnchangedSince(expected) : WritePrecondition.MustNotExist;
        var result = await SaveAsync(conflict.OriginalRelativePath, copy.Content, precondition, ct);
        if (result.Status != WriteStatus.Written) return ConflictResolutionStatus.OriginalChanged;
        await storage.DeleteAsync(conflict.ConflictRelativePath, ct);
        return ConflictResolutionStatus.Resolved;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_state == 2) return;
            _state = 2;
        }

        changes.Changed -= OnChange;
        changes.Dispose();
        _queue.Writer.TryComplete();
        await _shutdown.CancelAsync();
        if (_consumer is not null) await _consumer;
        _shutdown.Dispose();
    }

    private void OnChange(object? sender, VaultChange change) => _queue.Writer.TryWrite(change);

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(ct))
            {
                while (_queue.Reader.TryRead(out var change))
                {
                    // Varios reescaneos seguidos se atienden con una sola reconstrucción.
                    if (change.Kind == VaultChangeKind.Rescan)
                        while (_queue.Reader.TryPeek(out var next) && next.Kind == VaultChangeKind.Rescan)
                            _queue.Reader.TryRead(out _);
                    await ApplySafelyAsync(change, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cierre normal del servicio.
        }
    }

    private async Task ApplySafelyAsync(VaultChange change, CancellationToken ct)
    {
        try
        {
            await ApplyAsync(change, ct);
            RaiseChanged();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "No se pudo aplicar el cambio de {Path}", change.RelativePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Sin `ex`: leer o parsear una nota puede lanzar mensajes que citen su texto, y las notas pueden tener secretos.
            logger.LogError("Error inesperado ({ExceptionType}) al aplicar el cambio {Kind} de {Path}", ex.GetType().FullName, change.Kind, change.RelativePath);
        }
    }

    private async Task ApplyAsync(VaultChange change, CancellationToken ct)
    {
        if (change.Kind == VaultChangeKind.Rescan)
        {
            await RebuildAsync(ct);
            return;
        }
        if (VaultPaths.IsConflictFile(change.RelativePath))
        {
            await RefreshConflictsAsync(ct);
            return;
        }
        if (change.Kind == VaultChangeKind.Deleted)
        {
            lock (_gate) _index = _index.Remove(change.RelativePath);
            return;
        }

        var file = await ReadWithRetryAsync(change.RelativePath, ct);
        lock (_gate)
        {
            _index = file is null
                ? _index.Remove(change.RelativePath)
                : _index.Upsert(VaultEntityFactory.Create(file.RelativePath, file.Content, file.LastWriteTimeUtc));
        }
    }

    private async Task RebuildAsync(CancellationToken ct)
    {
        var files = await storage.ListMarkdownFilesAsync(ct);
        var entities = new ConcurrentBag<VaultEntity>();
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (file, token) =>
        {
            try
            {
                var content = await ReadWithRetryAsync(file.RelativePath, token);
                if (content is not null)
                    entities.Add(VaultEntityFactory.Create(content.RelativePath, content.Content, content.LastWriteTimeUtc));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Se omite la nota ilegible {Path}", file.RelativePath);
            }
        });

        var index = VaultIndex.Build(entities);
        lock (_gate) _index = index;
        await RefreshConflictsAsync(ct);
    }

    private async Task RefreshConflictsAsync(CancellationToken ct)
    {
        var conflicts = await storage.FindConflictsAsync(ct);
        lock (_gate) _conflicts = conflicts;
    }

    private async Task<VaultFileContent?> ReadWithRetryAsync(string relativePath, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await storage.ReadAsync(relativePath, ct);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException ex) when (attempt < MaxReadAttempts)
            {
                // Syncthing puede tener el fichero bloqueado un instante mientras lo sustituye.
                logger.LogDebug(ex, "Reintentando la lectura de {Path} (intento {Attempt})", relativePath, attempt);
                await Task.Delay(200 * attempt, ct);
            }
        }
    }

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
                // Sin `ex`: el suscriptor puede estar renderizando una nota y su mensaje citar texto.
                logger.LogError("Un suscriptor de VaultService.Changed ha lanzado una excepción ({ExceptionType})", ex.GetType().FullName);
            }
        }
    }
}
