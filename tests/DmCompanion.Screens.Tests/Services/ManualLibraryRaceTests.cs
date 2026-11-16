using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

/// <summary>Relistados concurrentes de <see cref="ManualLibrary"/> con un almacenamiento cuyo listado se puede retener o hacer fallar.</summary>
public sealed class ManualLibraryRaceTests : IAsyncDisposable
{
    private const string Pdf = "manuales/dnd5e/PHB.pdf";

    private readonly List<TempVault> _vaults = [];
    private readonly ScriptedFactory _factory = new();
    private readonly VaultHost _host;

    public ManualLibraryRaceTests() =>
        _host = new VaultHost(_factory, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    public async ValueTask DisposeAsync()
    {
        _factory.ReleaseAll();
        await _host.DisposeAsync();
        foreach (var vault in _vaults) vault.Dispose();
    }

    private TempVault NewVault()
    {
        var vault = TempVault.Empty();
        _vaults.Add(vault);
        Directory.CreateDirectory(vault.PathOf("manuales/dnd5e"));
        return vault;
    }

    private static void Put(TempVault vault, string relative) => File.WriteAllBytes(vault.PathOf(relative), [1, 2, 3]);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);
        condition().Should().BeTrue();
    }

    private async Task<(ManualLibrary Library, ScriptedStorage Storage)> OpenAsync(TempVault vault)
    {
        await _host.OpenAsync(vault.Root);
        var storage = _factory.Storage(vault.Root);
        var library = new ManualLibrary(_host, NullLogger<ManualLibrary>.Instance);
        await library.RefreshAsync();
        await WaitForAsync(() => storage.Completed >= 2);
        return (library, storage);
    }

    [Fact]
    public async Task Awaited_refresh_publishes_its_listing_even_if_a_newer_refresh_is_pending()
    {
        var vault = NewVault();
        var (library, storage) = await OpenAsync(vault);
        using var _ = library;
        Put(vault, Pdf);
        storage.Hold();
        var awaited = library.RefreshAsync();
        var newer = library.RefreshAsync();
        await WaitForAsync(() => storage.HeldCount >= 1);

        storage.ReleaseFirst();
        await awaited;

        library.Manuals.Select(m => m.Path).Should().Equal(Pdf);
        storage.ReleaseAll();
        await newer;
    }

    [Fact]
    public async Task Listing_failure_keeps_the_previous_list()
    {
        var vault = NewVault();
        Put(vault, Pdf);
        var (library, storage) = await OpenAsync(vault);
        using var _ = library;

        storage.FailWithIo = true;
        await library.RefreshAsync();

        library.Manuals.Select(m => m.Path).Should().Equal(Pdf);
    }

    [Fact]
    public async Task Listing_of_a_vault_that_was_replaced_is_discarded()
    {
        var first = NewVault();
        Put(first, "manuales/dnd5e/Viejo.pdf");
        var second = NewVault();
        Put(second, "manuales/dnd5e/Nuevo.pdf");
        var (library, storage) = await OpenAsync(first);
        using var _ = library;
        storage.Hold();
        var stale = library.RefreshAsync();
        await WaitForAsync(() => storage.HeldCount == 1);

        await _host.OpenAsync(second.Root);
        storage.ReleaseAll();
        await stale;

        await WaitForAsync(() => library.Manuals.Select(m => m.Path).SequenceEqual(["manuales/dnd5e/Nuevo.pdf"]));
    }

    private sealed class ScriptedFactory : IVaultServiceFactory
    {
        private readonly Dictionary<string, ScriptedStorage> _storages = [];

        public ScriptedStorage Storage(string root) => _storages[root];

        public void ReleaseAll()
        {
            foreach (var storage in _storages.Values) storage.ReleaseAll();
        }

        public VaultService Create(string rootPath)
        {
            var storage = _storages[rootPath] = new ScriptedStorage(new FileSystemVaultStorage(rootPath));
            return new VaultService(storage, new SilentSource(), NullLogger<VaultService>.Instance);
        }
    }

    private sealed class SilentSource : IVaultChangeSource
    {
#pragma warning disable CS0067 // Nunca emite: los relistados de estos tests son explícitos.
        public event EventHandler<VaultChange>? Changed;
#pragma warning restore CS0067
        public void Start() { }
        public void Dispose() { }
    }

    private sealed class ScriptedStorage(IVaultStorage inner) : IVaultStorage
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource> _held = [];
        private bool _holding;
        private int _completed;

        public volatile bool FailWithIo;

        public int Completed => Volatile.Read(ref _completed);

        public int HeldCount
        {
            get { lock (_gate) return _held.Count; }
        }

        public void Hold()
        {
            lock (_gate) _holding = true;
        }

        /// <summary>Suelta solo el primer listado retenido; los siguientes ya no se retienen.</summary>
        public void ReleaseFirst()
        {
            lock (_gate)
            {
                _holding = false;
                _held[0].TrySetResult();
            }
        }

        public void ReleaseAll()
        {
            lock (_gate)
            {
                _holding = false;
                foreach (var held in _held) held.TrySetResult();
            }
        }

        public async Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default)
        {
            try
            {
                Task? wait = null;
                lock (_gate)
                {
                    if (_holding)
                    {
                        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        _held.Add(held);
                        wait = held.Task;
                    }
                }
                if (wait is not null) await wait;
                if (FailWithIo) throw new IOException("listado simulado");
                return await inner.ListFilesAsync(f, ct);
            }
            finally
            {
                Interlocked.Increment(ref _completed);
            }
        }

        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public Task<VaultFileContent> ReadAsync(string p, CancellationToken ct = default) => inner.ReadAsync(p, ct);
        public Task<byte[]> ReadBytesAsync(string p, CancellationToken ct = default) => inner.ReadBytesAsync(p, ct);
        public Task<Stream> OpenReadAsync(string p, CancellationToken ct = default) => inner.OpenReadAsync(p, ct);
        public Task<VaultFileInfo?> StatAsync(string p, CancellationToken ct = default) => inner.StatAsync(p, ct);
        public Task<WriteResult> WriteAsync(string p, string c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteAsync(p, c, pre, ct);
        public Task<WriteResult> WriteBytesAsync(string p, Stream c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteBytesAsync(p, c, pre, ct);
        public Task<bool> MoveAsync(string from, string to, CancellationToken ct = default) => inner.MoveAsync(from, to, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string p, CancellationToken ct = default) => inner.DeleteAsync(p, ct);
    }
}
