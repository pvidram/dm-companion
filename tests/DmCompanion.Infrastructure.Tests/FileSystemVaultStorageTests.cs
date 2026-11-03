using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure.Tests;

public sealed class FileSystemVaultStorageTests : IDisposable
{
    private const string ConflictPath = "Lugares/Puerto Sal.sync-conflict-20261103-101500-ABCDEF1.md";
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FileSystemVaultStorage _storage;

    public FileSystemVaultStorageTests() => _storage = new FileSystemVaultStorage(_vault.Root);

    public void Dispose() => _vault.Dispose();

    [Fact]
    public async Task List_returns_only_indexable_markdown() =>
        (await _storage.ListMarkdownFilesAsync()).Should().HaveCount(8);

    [Fact]
    public async Task List_uses_forward_slash_relative_paths() =>
        (await _storage.ListMarkdownFilesAsync()).Select(f => f.RelativePath)
            .Should().Contain("Historia/Sesión 1 - Llegada a Puerto Sal.md");

    [Fact]
    public async Task Read_returns_content_and_timestamp()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        (file.Content.Contains("Lyra Vientoalto"), file.LastWriteTimeUtc)
            .Should().Be((true, File.GetLastWriteTimeUtc(_vault.PathOf("Personajes/Lyra.md"))));
    }

    [Fact]
    public async Task Stat_returns_length_and_timestamp()
    {
        var info = await _storage.StatAsync("Personajes/Lyra.md");
        info.Should().Be(new VaultFileInfo(new FileInfo(_vault.PathOf("Personajes/Lyra.md")).Length,
            File.GetLastWriteTimeUtc(_vault.PathOf("Personajes/Lyra.md"))));
    }

    [Fact]
    public async Task Stat_returns_null_for_missing_file() =>
        (await _storage.StatAsync("_assets/lyra.png")).Should().BeNull();

    [Fact]
    public async Task Stat_rejects_paths_outside_the_vault()
    {
        var act = () => _storage.StatAsync("../fuera.png");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Write_must_not_exist_creates_new_file_and_folders()
    {
        var result = await _storage.WriteAsync("Nueva/Nota.md", "hola", WritePrecondition.MustNotExist);
        (result.Status, File.ReadAllText(_vault.PathOf("Nueva/Nota.md"))).Should().Be((WriteStatus.Written, "hola"));
    }

    [Fact]
    public async Task Write_must_not_exist_refuses_existing_file()
    {
        var original = File.ReadAllText(_vault.PathOf("Personajes/Lyra.md"));
        var result = await _storage.WriteAsync("Personajes/Lyra.md", "pisado", WritePrecondition.MustNotExist);
        (result.Status, File.ReadAllText(_vault.PathOf("Personajes/Lyra.md"))).Should().Be((WriteStatus.AlreadyExists, original));
    }

    [Fact]
    public async Task Write_unchanged_since_succeeds_when_timestamp_matches()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        var result = await _storage.WriteAsync(file.RelativePath, "editado", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));
        result.Status.Should().Be(WriteStatus.Written);
    }

    [Fact]
    public async Task Write_unchanged_since_detects_external_change()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        File.WriteAllText(_vault.PathOf(file.RelativePath), "cambio desde Syncthing");
        File.SetLastWriteTimeUtc(_vault.PathOf(file.RelativePath), file.LastWriteTimeUtc.AddMinutes(1));

        var result = await _storage.WriteAsync(file.RelativePath, "mi versión", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));

        (result.Status, File.ReadAllText(_vault.PathOf(file.RelativePath))).Should().Be((WriteStatus.ModifiedOnDisk, "cambio desde Syncthing"));
    }

    [Fact]
    public async Task Write_leaves_no_temporary_files()
    {
        await _storage.WriteAsync("Personajes/Lyra.md", "x", WritePrecondition.Overwrite);
        Directory.EnumerateFiles(_vault.PathOf("Personajes"), "*.dmc.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task FindConflicts_pairs_conflict_with_original() =>
        (await _storage.FindConflictsAsync()).Should().ContainSingle()
            .Which.Should().Be(new SyncConflict("Lugares/Puerto Sal.md", ConflictPath));

    [Fact]
    public async Task Cancelled_write_throws_and_leaves_no_temporary_files()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var act = () => _storage.WriteAsync("Personajes/Nueva.md", "x", WritePrecondition.Overwrite, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.EnumerateFiles(_vault.PathOf("Personajes"), "*.dmc.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Write_unchanged_since_on_deleted_file_reports_modified_and_does_not_recreate()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        File.Delete(_vault.PathOf(file.RelativePath));

        var result = await _storage.WriteAsync(file.RelativePath, "x", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));

        (result.Status, File.Exists(_vault.PathOf(file.RelativePath))).Should().Be((WriteStatus.ModifiedOnDisk, false));
    }

    [Fact]
    public async Task Concurrent_writes_to_same_file_leave_no_temporary_files()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            _storage.WriteAsync("Personajes/Lyra.md", $"v{i}", WritePrecondition.Overwrite)));
        Directory.EnumerateFiles(_vault.Root, "*.dmc.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_file_with_unchanged_since_is_not_created()
    {
        var result = await _storage.WriteAsync("Nueva/Otra.md", "x", WritePrecondition.UnchangedSince(DateTime.UtcNow));
        (result.Status, File.Exists(_vault.PathOf("Nueva/Otra.md"))).Should().Be((WriteStatus.ModifiedOnDisk, false));
    }

    [Fact]
    public async Task Delete_removes_file()
    {
        await _storage.DeleteAsync(ConflictPath);
        File.Exists(_vault.PathOf(ConflictPath)).Should().BeFalse();
    }

    [Fact]
    public async Task Paths_outside_the_vault_are_rejected()
    {
        var act = () => _storage.ReadAsync("../fuera.md");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Missing_root_is_rejected()
    {
        var act = () => new FileSystemVaultStorage(Path.Combine(_vault.Root, "no-existe"));
        act.Should().Throw<DirectoryNotFoundException>();
    }
}
