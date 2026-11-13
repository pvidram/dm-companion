using System.Collections.Concurrent;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure.Tests;

public sealed class FileSystemVaultWatcherTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly TempVault _vault = TempVault.Empty();
    private readonly FileSystemVaultWatcher _watcher;
    private readonly ConcurrentQueue<VaultChange> _changes = new();

    public FileSystemVaultWatcherTests()
    {
        _watcher = new FileSystemVaultWatcher(_vault.Root);
        _watcher.Changed += (_, change) => _changes.Enqueue(change);
        _watcher.Start();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _vault.Dispose();
    }

    private async Task<bool> EventuallyAsync(Func<VaultChange, bool> predicate)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_changes.Any(predicate)) return true;
            await Task.Delay(50);
        }
        return false;
    }

    private string Seen() => string.Join(", ", _changes.Select(c => $"{c.Kind}:{c.RelativePath}"));

    [Fact]
    public async Task Creating_a_note_raises_upserted()
    {
        await File.WriteAllTextAsync(_vault.PathOf("Nueva.md"), "x");
        (await EventuallyAsync(c => c is { RelativePath: "Nueva.md", Kind: VaultChangeKind.Upserted })).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Deleting_a_note_raises_deleted()
    {
        await File.WriteAllTextAsync(_vault.PathOf("Borrar.md"), "x");
        File.Delete(_vault.PathOf("Borrar.md"));
        (await EventuallyAsync(c => c is { RelativePath: "Borrar.md", Kind: VaultChangeKind.Deleted })).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Atomic_rename_from_temp_file_raises_upserted_for_the_note()
    {
        await File.WriteAllTextAsync(_vault.PathOf(".Nota.md.dmc.tmp"), "x");
        File.Move(_vault.PathOf(".Nota.md.dmc.tmp"), _vault.PathOf("Nota.md"));
        (await EventuallyAsync(c => c is { RelativePath: "Nota.md", Kind: VaultChangeKind.Upserted })).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Changes_in_subfolders_use_relative_paths()
    {
        Directory.CreateDirectory(_vault.PathOf("Lugares"));
        await File.WriteAllTextAsync(_vault.PathOf("Lugares/Puerto.md"), "x");
        (await EventuallyAsync(c => c.RelativePath == "Lugares/Puerto.md")).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Renaming_a_folder_raises_rescan()
    {
        Directory.CreateDirectory(_vault.PathOf("Viejo"));
        await Task.Delay(200);
        Directory.Move(_vault.PathOf("Viejo"), _vault.PathOf("Nuevo"));
        (await EventuallyAsync(c => c.Kind == VaultChangeKind.Rescan)).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Deleting_a_folder_raises_rescan()
    {
        Directory.CreateDirectory(_vault.PathOf("Viejo"));
        await File.WriteAllTextAsync(_vault.PathOf("Viejo/Nota.md"), "x");
        await Task.Delay(200);
        Directory.Delete(_vault.PathOf("Viejo"), recursive: true);
        (await EventuallyAsync(c => c.Kind == VaultChangeKind.Rescan)).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Writing_a_state_file_raises_upserted()
    {
        Directory.CreateDirectory(_vault.PathOf("Mapas"));
        await File.WriteAllTextAsync(_vault.PathOf("Mapas/X.state.json"), "{}");
        (await EventuallyAsync(c => c is { RelativePath: "Mapas/X.state.json", Kind: VaultChangeKind.Upserted })).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Writing_an_image_raises_upserted()
    {
        Directory.CreateDirectory(_vault.PathOf("Mapas"));
        await File.WriteAllBytesAsync(_vault.PathOf("Mapas/x.png"), [1, 2, 3]);
        (await EventuallyAsync(c => c is { RelativePath: "Mapas/x.png", Kind: VaultChangeKind.Upserted })).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Deleting_an_image_raises_deleted_not_a_rescan()
    {
        await File.WriteAllBytesAsync(_vault.PathOf("x.png"), [1]);
        File.Delete(_vault.PathOf("x.png"));
        (await EventuallyAsync(c => c is { RelativePath: "x.png", Kind: VaultChangeKind.Deleted })).Should().BeTrue("llegó: {0}", Seen());
    }

    [Fact]
    public async Task Writing_an_untracked_file_raises_nothing()
    {
        await File.WriteAllTextAsync(_vault.PathOf("notas.txt"), "x");
        await File.WriteAllTextAsync(_vault.PathOf("Marca.md"), "x"); // testigo: cuando llega, ya habría llegado notas.txt
        (await EventuallyAsync(c => c.RelativePath == "Marca.md")).Should().BeTrue();
        _changes.Should().NotContain(c => c.RelativePath == "notas.txt");
    }

    [Fact]
    public async Task Writing_a_syncthing_state_conflict_raises_upserted()
    {
        Directory.CreateDirectory(_vault.PathOf("Mapas"));
        const string conflict = "Mapas/X.state.sync-conflict-20261112-101500-ABCDEF1.json";
        await File.WriteAllTextAsync(_vault.PathOf(conflict), "{}");
        (await EventuallyAsync(c => c is { RelativePath: conflict, Kind: VaultChangeKind.Upserted })).Should().BeTrue("llegó: {0}", Seen());
    }
}
