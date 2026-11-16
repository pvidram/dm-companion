using DmCompanion.Core.Manuals;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public sealed class ManualBookmarkStoreTests : IAsyncDisposable
{
    private const string Pdf = "manuales/dnd5e/PHB.pdf";
    private const string Json = "manuales/dnd5e/PHB.marcadores.json";

    private readonly TempVault _vault = TempVault.Empty();
    private readonly RacingFactory _racing = new();
    private readonly VaultHost _host;

    public ManualBookmarkStoreTests() =>
        _host = new(_racing, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private async Task<ManualBookmarkStore> StoreAsync()
    {
        Directory.CreateDirectory(_vault.PathOf("manuales/dnd5e"));
        await _host.OpenAsync(_vault.Root);
        return new ManualBookmarkStore(_host, NullLogger<ManualBookmarkStore>.Instance);
    }

    private ManualBookmarkFile OnDisk() => ManualBookmarks.Parse(File.ReadAllText(_vault.PathOf(Json))).File!;

    [Fact]
    public async Task First_bookmark_creates_the_file()
    {
        var store = await StoreAsync();

        var (result, file) = await store.AddAsync(Pdf, "Combate", 5);

        (result, file!.Bookmarks.Single().Page, OnDisk().Bookmarks.Single().Name).Should().BeEquivalentTo((BookmarkSaveResult.Saved, 5, "Combate"));
    }

    [Fact]
    public async Task Adding_keeps_existing()
    {
        var store = await StoreAsync();
        await store.AddAsync(Pdf, "Uno", 1);

        await store.AddAsync(Pdf, "Dos", 2);

        OnDisk().Bookmarks.Select(b => b.Name).Should().Equal("Uno", "Dos");
    }

    [Fact]
    public async Task External_change_is_merged_not_overwritten()
    {
        var store = await StoreAsync();
        await store.AddAsync(Pdf, "Uno", 1);
        // Otro dispositivo escribe entre nuestra lectura y nuestra escritura.
        var external = new ManualBookmarkFile(1, [.. OnDisk().Bookmarks, new ManualBookmark("ext", "Externo", 9)]);
        _racing.BeforeNextWrite = () =>
        {
            File.WriteAllText(_vault.PathOf(Json), ManualBookmarks.Serialize(external));
            File.SetLastWriteTimeUtc(_vault.PathOf(Json), DateTime.UtcNow.AddMinutes(5));
        };
        var racing = store;

        var (result, _) = await racing.AddAsync(Pdf, "Dos", 2);

        (result, OnDisk().Bookmarks.Select(b => b.Name).ToArray()).Should().BeEquivalentTo((BookmarkSaveResult.Saved, new[] { "Uno", "Externo", "Dos" }), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Corrupt_file_is_never_overwritten()
    {
        var store = await StoreAsync();
        File.WriteAllText(_vault.PathOf(Json), "{ no es json");

        var (result, _) = await store.AddAsync(Pdf, "Uno", 1);

        (result, File.ReadAllText(_vault.PathOf(Json)), store.CorruptFiles.ToArray())
            .Should().BeEquivalentTo((BookmarkSaveResult.Corrupt, "{ no es json", new[] { Json }));
    }

    [Fact]
    public async Task Switching_vault_clears_the_corrupt_files()
    {
        var store = await StoreAsync();
        File.WriteAllText(_vault.PathOf(Json), "{ no es json");
        await store.LoadAsync(Pdf);
        using var other = TempVault.Empty();

        await _host.OpenAsync(other.Root);

        store.CorruptFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_deletes_by_id()
    {
        var store = await StoreAsync();
        var (_, file) = await store.AddAsync(Pdf, "Uno", 1);
        await store.AddAsync(Pdf, "Dos", 2);

        var (result, after) = await store.RemoveAsync(Pdf, file!.Bookmarks.Single().Id);

        (result, after!.Bookmarks.Select(b => b.Name).ToArray()).Should().BeEquivalentTo((BookmarkSaveResult.Saved, new[] { "Dos" }));
    }

    private sealed class RacingFactory : IVaultServiceFactory
    {
        public Action? BeforeNextWrite;

        public VaultService Create(string rootPath) => new(
            new RacingStorage(new FileSystemVaultStorage(rootPath), this),
            new FileSystemVaultWatcher(rootPath),
            NullLogger<VaultService>.Instance);
    }

    private sealed class RacingStorage(FileSystemVaultStorage inner, RacingFactory owner) : IVaultStorage
    {
        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public Task<VaultFileContent> ReadAsync(string p, CancellationToken ct = default) => inner.ReadAsync(p, ct);
        public Task<byte[]> ReadBytesAsync(string p, CancellationToken ct = default) => inner.ReadBytesAsync(p, ct);
        public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default) => inner.ListFilesAsync(f, ct);
        public Task<Stream> OpenReadAsync(string p, CancellationToken ct = default) => inner.OpenReadAsync(p, ct);
        public Task<VaultFileInfo?> StatAsync(string p, CancellationToken ct = default) => inner.StatAsync(p, ct);
        public Task<WriteResult> WriteBytesAsync(string p, Stream c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteBytesAsync(p, c, pre, ct);
        public Task<bool> MoveAsync(string a, string b, CancellationToken ct = default) => inner.MoveAsync(a, b, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string p, CancellationToken ct = default) => inner.DeleteAsync(p, ct);

        public Task<WriteResult> WriteAsync(string p, string c, WritePrecondition pre, CancellationToken ct = default)
        {
            Interlocked.Exchange(ref owner.BeforeNextWrite, null)?.Invoke();
            return inner.WriteAsync(p, c, pre, ct);
        }
    }
}
