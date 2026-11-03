using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Infrastructure.Tests;

public sealed class VaultServiceTests : IAsyncLifetime
{
    private const string ConflictPath = "Lugares/Puerto Sal.sync-conflict-20261103-101500-ABCDEF1.md";
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly ManualChangeSource _changes = new();
    private VaultService _service = null!;

    public async Task InitializeAsync()
    {
        _service = new VaultService(new FileSystemVaultStorage(_vault.Root), _changes, NullLogger<VaultService>.Instance);
        await _service.LoadAsync();
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        _vault.Dispose();
    }

    private Task WaitUntilAsync(Func<bool> condition)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Changed += (_, _) => { if (condition()) done.TrySetResult(); };
        if (condition()) done.TrySetResult();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Load_indexes_demo_vault() => _service.Index.Count.Should().Be(8);

    [Fact]
    public void Load_reads_entity_titles() =>
        _service.Index.Get("Personajes/Lyra.md")!.Title.Should().Be("Lyra Vientoalto");

    [Fact]
    public void Load_finds_conflicts() => _service.Conflicts.Should().ContainSingle();

    [Fact]
    public void Load_starts_watching() => _changes.Started.Should().BeTrue();

    [Fact]
    public async Task Upserted_change_reloads_the_file()
    {
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra.md"), "---\nname: Lyra Renombrada\n---\n");
        _changes.Raise("Personajes/Lyra.md", VaultChangeKind.Upserted);
        await WaitUntilAsync(() => _service.Index.Get("Personajes/Lyra.md")?.Title == "Lyra Renombrada");
    }

    [Fact]
    public async Task Deleted_change_removes_the_entity()
    {
        File.Delete(_vault.PathOf("Personajes/Lyra.md"));
        _changes.Raise("Personajes/Lyra.md", VaultChangeKind.Deleted);
        await WaitUntilAsync(() => _service.Index.Get("Personajes/Lyra.md") is null);
    }

    [Fact]
    public async Task Upserted_change_for_vanished_file_removes_the_entity()
    {
        File.Delete(_vault.PathOf("Personajes/Lyra.md"));
        _changes.Raise("Personajes/Lyra.md", VaultChangeKind.Upserted);
        await WaitUntilAsync(() => _service.Index.Get("Personajes/Lyra.md") is null);
    }

    [Fact]
    public async Task Conflict_file_change_refreshes_conflicts()
    {
        File.Delete(_vault.PathOf(ConflictPath));
        _changes.Raise(ConflictPath, VaultChangeKind.Deleted);
        await WaitUntilAsync(() => _service.Conflicts.Count == 0);
    }

    [Fact]
    public async Task Rescan_rebuilds_the_index()
    {
        await File.WriteAllTextAsync(_vault.PathOf("Historia/Extra.md"), "x");
        _changes.Raise(string.Empty, VaultChangeKind.Rescan);
        await WaitUntilAsync(() => _service.Index.Count == 9);
    }

    [Fact]
    public async Task Save_updates_index_immediately()
    {
        var file = await _service.ReadRawAsync("Personajes/Lyra.md");
        await _service.SaveAsync(file.RelativePath, "---\nname: Lyra Guardada\n---\n", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));
        _service.Index.Get("Personajes/Lyra.md")!.Title.Should().Be("Lyra Guardada");
    }

    [Fact]
    public async Task Save_of_hidden_path_is_written()
    {
        var result = await _service.SaveAsync(".oculto.md", "x", WritePrecondition.Overwrite);
        result.Status.Should().Be(WriteStatus.Written);
    }

    [Fact]
    public async Task Save_of_hidden_path_is_not_indexed()
    {
        await _service.SaveAsync(".oculto.md", "x", WritePrecondition.Overwrite);
        _service.Index.Get(".oculto.md").Should().BeNull();
    }

    [Fact]
    public async Task Save_with_stale_timestamp_keeps_index()
    {
        var result = await _service.SaveAsync("Personajes/Lyra.md", "---\nname: Pisado\n---\n", WritePrecondition.UnchangedSince(DateTime.UnixEpoch));
        (result.Status, _service.Index.Get("Personajes/Lyra.md")!.Title).Should().Be((WriteStatus.ModifiedOnDisk, "Lyra Vientoalto"));
    }

    [Fact]
    public async Task CreateNote_uses_default_folder_and_indexes_it()
    {
        var result = await _service.CreateNoteAsync(EntityType.Npc, "Mercader Olm");
        (result.RelativePath, result.Status, _service.Index.Get(result.RelativePath)!.Type)
            .Should().Be(("Personajes/Mercader Olm.md", WriteStatus.Written, EntityType.Npc));
    }

    [Fact]
    public async Task CreateNote_refuses_existing_note()
    {
        var result = await _service.CreateNoteAsync(EntityType.Pc, "Lyra");
        result.Status.Should().Be(WriteStatus.AlreadyExists);
    }

    private const string OriginalPath = "Lugares/Puerto Sal.md";

    private async Task<DateTime> OriginalStampAsync() => (await _service.ReadRawAsync(OriginalPath)).LastWriteTimeUtc;

    [Fact]
    public async Task ResolveConflict_keeping_copy_overwrites_original_and_deletes_copy()
    {
        var copy = await File.ReadAllTextAsync(_vault.PathOf(ConflictPath));
        await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepConflictCopy, await OriginalStampAsync());
        (File.ReadAllText(_vault.PathOf(OriginalPath)), File.Exists(_vault.PathOf(ConflictPath)), _service.Conflicts.Count)
            .Should().Be((copy, false, 0));
    }

    [Fact]
    public async Task ResolveConflict_keeping_original_only_deletes_copy()
    {
        var original = await File.ReadAllTextAsync(_vault.PathOf(OriginalPath));
        await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepOriginal, await OriginalStampAsync());
        (File.ReadAllText(_vault.PathOf(OriginalPath)), File.Exists(_vault.PathOf(ConflictPath)))
            .Should().Be((original, false));
    }

    [Fact]
    public async Task ResolveConflict_keeping_copy_reports_resolved()
    {
        var result = await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepConflictCopy, await OriginalStampAsync());
        result.Status.Should().Be(ConflictResolutionStatus.Resolved);
    }

    [Fact]
    public async Task ResolveConflict_keeping_copy_after_original_changed_reports_it()
    {
        var stamp = await OriginalStampAsync();
        await File.WriteAllTextAsync(_vault.PathOf(OriginalPath), "editado después de mostrar el conflicto");
        File.SetLastWriteTimeUtc(_vault.PathOf(OriginalPath), stamp.AddMinutes(5));
        var result = await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepConflictCopy, stamp);
        result.Status.Should().Be(ConflictResolutionStatus.OriginalChanged);
    }

    [Fact]
    public async Task ResolveConflict_keeping_copy_after_original_changed_keeps_both_files()
    {
        var stamp = await OriginalStampAsync();
        await File.WriteAllTextAsync(_vault.PathOf(OriginalPath), "editado después de mostrar el conflicto");
        File.SetLastWriteTimeUtc(_vault.PathOf(OriginalPath), stamp.AddMinutes(5));
        await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepConflictCopy, stamp);
        (File.ReadAllText(_vault.PathOf(OriginalPath)), File.Exists(_vault.PathOf(ConflictPath)))
            .Should().Be(("editado después de mostrar el conflicto", true));
    }

    [Fact]
    public async Task ResolveConflict_keeping_original_when_it_was_missing_reports_it()
    {
        var result = await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepOriginal, null);
        result.Status.Should().Be(ConflictResolutionStatus.OriginalMissing);
    }

    [Fact]
    public async Task ResolveConflict_keeping_original_when_it_was_missing_keeps_the_copy()
    {
        await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepOriginal, null);
        File.Exists(_vault.PathOf(ConflictPath)).Should().BeTrue();
    }

    [Fact]
    public async Task ResolveConflict_keeping_original_deleted_after_viewing_reports_missing()
    {
        var stamp = await OriginalStampAsync();
        File.Delete(_vault.PathOf(OriginalPath));
        var result = await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepOriginal, stamp);
        result.Status.Should().Be(ConflictResolutionStatus.OriginalMissing);
    }

    [Fact]
    public async Task ResolveConflict_keeping_original_deleted_after_viewing_keeps_the_copy()
    {
        var stamp = await OriginalStampAsync();
        File.Delete(_vault.PathOf(OriginalPath));
        await _service.ResolveConflictAsync(_service.Conflicts[0], ConflictChoice.KeepOriginal, stamp);
        File.Exists(_vault.PathOf(ConflictPath)).Should().BeTrue();
    }

    [Fact]
    public async Task ResolveConflict_refuses_non_conflict_paths()
    {
        var bogus = new SyncConflict("Lugares/Puerto Sal.md", "Personajes/Lyra.md");
        var act = async () => await _service.ResolveConflictAsync(bogus, ConflictChoice.KeepOriginal, DateTime.UtcNow);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Throwing_subscriber_does_not_break_live_updates()
    {
        _service.Changed += (_, _) => throw new InvalidOperationException("suscriptor roto");
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra.md"), "---\nname: Lyra Nueva\n---\n");
        _changes.Raise("Personajes/Lyra.md", VaultChangeKind.Upserted);
        await WaitUntilAsync(() => _service.Index.Get("Personajes/Lyra.md")?.Title == "Lyra Nueva");

        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra.md"), "---\nname: Lyra Otra Vez\n---\n");
        _changes.Raise("Personajes/Lyra.md", VaultChangeKind.Upserted);
        await WaitUntilAsync(() => _service.Index.Get("Personajes/Lyra.md")?.Title == "Lyra Otra Vez");
    }

    [Fact]
    public async Task Save_with_throwing_subscriber_still_returns_written()
    {
        _service.Changed += (_, _) => throw new InvalidOperationException("suscriptor roto");
        var result = await _service.SaveAsync("Personajes/Lyra.md", "---\nname: Guardada\n---\n", WritePrecondition.Overwrite);
        result.Status.Should().Be(WriteStatus.Written);
    }

    [Fact]
    public async Task Dispose_twice_does_not_throw()
    {
        await _service.DisposeAsync();
        var act = async () => await _service.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Load_twice_subscribes_once()
    {
        await _service.LoadAsync();
        _changes.SubscriberCount.Should().Be(1);
    }

    [Fact]
    public async Task Load_after_dispose_throws()
    {
        await _service.DisposeAsync();
        var act = async () => await _service.LoadAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Real_watcher_picks_up_external_edits()
    {
        await using var service = new VaultService(new FileSystemVaultStorage(_vault.Root), new FileSystemVaultWatcher(_vault.Root), NullLogger<VaultService>.Instance);
        await service.LoadAsync();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Changed += (_, _) => { if (service.Index.Get("Historia/Externa.md") is not null) done.TrySetResult(); };

        await File.WriteAllTextAsync(_vault.PathOf("Historia/Externa.md"), "Escrita por Syncthing");

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Failed_load_unsubscribes_from_changes()
    {
        var changes = new ManualChangeSource();
        var service = new VaultService(new FaultyStorage(_vault.Root) { ListFailure = new IOException("disco") }, changes, NullLogger<VaultService>.Instance);
        try { await service.LoadAsync(); } catch (IOException) { }
        changes.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public async Task Failed_load_rethrows_the_error()
    {
        var service = new VaultService(new FaultyStorage(_vault.Root) { ListFailure = new IOException("disco") }, new ManualChangeSource(), NullLogger<VaultService>.Instance);
        var act = async () => await service.LoadAsync();
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task Load_after_failed_load_throws_object_disposed()
    {
        var service = new VaultService(new FaultyStorage(_vault.Root) { ListFailure = new IOException("disco") }, new ManualChangeSource(), NullLogger<VaultService>.Instance);
        try { await service.LoadAsync(); } catch (IOException) { }
        var act = async () => await service.LoadAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_after_failed_load_does_not_throw()
    {
        var service = new VaultService(new FaultyStorage(_vault.Root) { ListFailure = new IOException("disco") }, new ManualChangeSource(), NullLogger<VaultService>.Instance);
        try { await service.LoadAsync(); } catch (IOException) { }
        var act = async () => await service.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Load_skips_unreadable_note_and_indexes_the_rest()
    {
        var storage = new FaultyStorage(_vault.Root) { UnreadablePath = "Personajes/Lyra.md" };
        await using var service = new VaultService(storage, new ManualChangeSource(), NullLogger<VaultService>.Instance);
        await service.LoadAsync();
        (service.Index.Get("Personajes/Lyra.md"), service.Index.Count).Should().Be((null, 7));
    }

    [Fact]
    public async Task Unexpected_apply_error_logs_path_and_exception_type_without_its_message()
    {
        var log = await ApplyFailureLogAsync();
        log.Should().Contain("Personajes/Lyra.md").And.Contain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task Unexpected_apply_error_log_does_not_contain_text_quoted_by_the_exception()
    {
        var log = await ApplyFailureLogAsync();
        log.Should().NotContain(SecretMarker);
    }

    private const string SecretMarker = "SECRETO-9f3a71c2";

    // El mensaje de la excepción simula un parser que cita el texto de la nota.
    private async Task<string> ApplyFailureLogAsync()
    {
        var logger = new TextLogger<VaultService>();
        var changes = new ManualChangeSource();
        var storage = new FaultyStorage(_vault.Root);
        await using var service = new VaultService(storage, changes, logger);
        await service.LoadAsync();
        storage.ReadFailure = new InvalidOperationException($"error cerca de «{SecretMarker}»");
        changes.Raise("Personajes/Lyra.md", VaultChangeKind.Upserted);
        await Task.Run(async () => { while (!logger.Text.Contains("Lyra.md")) await Task.Delay(10); }).WaitAsync(TimeSpan.FromSeconds(5));
        return logger.Text;
    }

    private sealed class TextLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();
        public string Text => string.Join('\n', _lines);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Enqueue($"{formatter(state, exception)}\n{exception}");
    }

    private sealed class FaultyStorage(string root) : IVaultStorage
    {
        private readonly FileSystemVaultStorage _inner = new(root);

        public Exception? ListFailure { get; init; }
        public string? UnreadablePath { get; init; }
        public Exception? ReadFailure { get; set; }

        public string RootPath => _inner.RootPath;

        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) =>
            ListFailure is null ? _inner.ListMarkdownFilesAsync(ct) : Task.FromException<IReadOnlyList<VaultFile>>(ListFailure);

        public Task<VaultFileContent> ReadAsync(string relativePath, CancellationToken ct = default) =>
            ReadFailure is not null && relativePath == "Personajes/Lyra.md"
                ? Task.FromException<VaultFileContent>(ReadFailure)
                : relativePath == UnreadablePath
                ? Task.FromException<VaultFileContent>(new UnauthorizedAccessException("sin permiso"))
                : _inner.ReadAsync(relativePath, ct);

        public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default) => _inner.ReadBytesAsync(relativePath, ct);

        public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default) => _inner.StatAsync(relativePath, ct);

        public Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default) =>
            _inner.WriteAsync(relativePath, content, precondition, ct);

        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => _inner.FindConflictsAsync(ct);

        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => _inner.DeleteAsync(relativePath, ct);
    }
}
