using System.Collections.Concurrent;
using System.Threading.Channels;
using DmCompanion.Core.GameSystems;
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
    private CampaignInfo _campaign = CampaignResolver.Resolve(VaultIndex.Empty);
    private IReadOnlyList<SyncConflict> _conflicts = [];

    public event EventHandler? Changed;

    // Cambios de ficheros que no son notas Markdown (estado del mapa, imágenes). No reindexan.
    public event EventHandler<VaultChange>? FileChanged;

    public string RootPath => storage.RootPath;

    public VaultIndex Index { get { lock (_gate) return _index; } }

    public CampaignInfo Campaign { get { lock (_gate) return _campaign; } }

    public IGameSystem ActiveSystem => GameSystemRegistry.Default.Get(Campaign.SystemId);

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

    public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> predicate, CancellationToken ct = default) =>
        storage.ListFilesAsync(predicate, ct);

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) =>
        storage.OpenReadAsync(relativePath, ct);

    public Task<WriteResult> WriteBytesAsync(string relativePath, Stream content, WritePrecondition precondition, CancellationToken ct = default) =>
        storage.WriteBytesAsync(relativePath, content, precondition, ct);

    // Mover o borrar una nota la quita ya del índice, sin esperar al watcher: el panel de mapas relee el catálogo justo después.
    public async Task<bool> MoveAsync(string fromRelativePath, string toRelativePath, CancellationToken ct = default)
    {
        var moved = await storage.MoveAsync(fromRelativePath, toRelativePath, ct);
        if (!moved) return false;
        // El destino se indexa ya: quien mueve una nota puede necesitarla en el índice antes de que el watcher la vea.
        if (VaultPaths.IsIndexable(toRelativePath))
        {
            try
            {
                var file = await storage.ReadAsync(toRelativePath, ct);
                SetIndex(current => current.Upsert(VaultEntityFactory.Create(file.RelativePath, file.Content, file.LastWriteTimeUtc)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("No se pudo indexar {Path} tras moverla; lo hará el watcher ({ExceptionType})", toRelativePath, ex.GetType().FullName);
            }
        }
        ForgetIndexed(fromRelativePath);
        return true;
    }

    public async Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        await storage.DeleteAsync(relativePath, ct);
        ForgetIndexed(relativePath);
    }

    private void ForgetIndexed(string relativePath)
    {
        if (!VaultPaths.IsIndexable(relativePath)) return;
        SetIndex(current => current.Remove(relativePath));
        RaiseChanged();
    }

    // Escritura de ficheros de texto que no son notas (el .state.json del mapa): sin reindexar.
    public Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default) =>
        storage.WriteAsync(relativePath, content, precondition, ct);

    public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default) =>
        storage.StatAsync(relativePath, ct);

    public async Task<WriteResult> SaveAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default)
    {
        var result = await storage.WriteAsync(relativePath, content, precondition, ct);
        if (result.Status != WriteStatus.Written) return result;

        if (VaultPaths.IsIndexable(relativePath))
        {
            var entity = VaultEntityFactory.Create(relativePath, content, result.LastWriteTimeUtc);
            SetIndex(current => current.Upsert(entity));
        }
        RaiseChanged();
        return result;
    }

    public async Task<CreateNoteResult> CreateNoteAsync(EntityType type, string title, string? folder = null, CancellationToken ct = default)
    {
        var directory = VaultPaths.Normalize(folder ?? NoteTemplates.DefaultFolder(type)).TrimEnd('/');
        var fileName = NoteFileName.FromTitle(title) + ".md";
        var relativePath = directory.Length == 0 ? fileName : $"{directory}/{fileName}";
        var result = await SaveAsync(relativePath, NoteTemplates.Create(type, title, ActiveSystem), WritePrecondition.MustNotExist, ct);
        return new CreateNoteResult(relativePath, result.Status);
    }

    public async Task<CreateNoteResult> CreatePlayerCharacterAsync(PcDraft draft, CancellationToken ct = default)
    {
        var errors = draft.Validate(ActiveSystem);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(draft));

        var directory = NoteTemplates.DefaultFolder(EntityType.Pc);
        var relativePath = $"{directory}/{NoteFileName.FromTitle(draft.Name)}.md";
        var result = await SaveAsync(relativePath, NoteTemplates.CreatePc(draft, ActiveSystem), WritePrecondition.MustNotExist, ct);
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
            logger.LogWarning("El original de una copia en conflicto ya no existe; se conserva la copia ({ExceptionType})", ex.GetType().Name);
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
            if (change.Kind != VaultChangeKind.Rescan && !VaultPaths.IsMarkdown(change.RelativePath))
            {
                await ApplyFileAsync(change, ct);
                return;
            }
            await ApplyAsync(change, ct);
            RaiseChanged();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo aplicar un cambio del vault ({ExceptionType})", ex.GetType().Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Sin `ex`: leer o parsear una nota puede lanzar mensajes que citen su texto, y las notas pueden tener secretos.
            logger.LogError("Error inesperado ({ExceptionType}) al aplicar el cambio {Kind} de {Path}", ex.GetType().FullName, change.Kind, change.RelativePath);
        }
    }

    private async Task ApplyFileAsync(VaultChange change, CancellationToken ct)
    {
        if (VaultPaths.IsConflictFile(change.RelativePath))
        {
            await RefreshConflictsAsync(ct);
            RaiseChanged();
        }
        RaiseFileChanged(change);
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
            SetIndex(current => current.Remove(change.RelativePath));
            return;
        }

        var file = await ReadWithRetryAsync(change.RelativePath, ct);
        SetIndex(current => file is null
            ? current.Remove(change.RelativePath)
            : current.Upsert(VaultEntityFactory.Create(file.RelativePath, file.Content, file.LastWriteTimeUtc)));
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
                logger.LogWarning("Se omite una nota ilegible ({ExceptionType})", ex.GetType().Name);
            }
        });

        var index = VaultIndex.Build(entities);
        SetIndex(_ => index);
        await RefreshConflictsAsync(ct);
    }

    // Único punto de escritura del índice: la campaña se recalcula siempre con él, bajo el mismo bloqueo.
    private void SetIndex(Func<VaultIndex, VaultIndex> update)
    {
        lock (_gate)
        {
            _index = update(_index);
            _campaign = CampaignResolver.Resolve(_index);
        }
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
                logger.LogDebug("Reintentando la lectura de una nota (intento {Attempt}, {ExceptionType})", attempt, ex.GetType().Name);
                await Task.Delay(200 * attempt, ct);
            }
        }
    }

    private void RaiseFileChanged(VaultChange change)
    {
        var handlers = FileChanged?.GetInvocationList();
        if (handlers is null) return;
        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler<VaultChange>)handler)(this, change);
            }
            catch (Exception ex)
            {
                logger.LogError("Un suscriptor de VaultService.FileChanged ha lanzado una excepción ({ExceptionType})", ex.GetType().FullName);
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
