using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class AudioLibraryServiceRebuildTests : IAsyncDisposable
{
    private readonly List<TempVault> _vaults = [];
    private readonly GatedFactory _factory = new();
    private readonly VaultHost _host;

    public AudioLibraryServiceRebuildTests() =>
        _host = new VaultHost(_factory, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        foreach (var vault in _vaults) vault.Dispose();
    }

    private TempVault NewVault(params string[] audioFiles)
    {
        var vault = TempVault.Empty();
        _vaults.Add(vault);
        foreach (var file in audioFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(vault.PathOf(file))!);
            File.WriteAllBytes(vault.PathOf(file), [1]);
        }
        return vault;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);
        condition().Should().BeTrue();
    }

    [Fact]
    public async Task Many_events_in_a_row_are_coalesced_into_at_most_two_listings()
    {
        var vault = NewVault("_audio/a.mp3");
        var gated = _factory.Gate(vault.Root);
        (await _host.OpenAsync(vault.Root)).Should().BeTrue();
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);
        await WaitForAsync(() => gated.ListCalls == 1);

        for (var i = 0; i < 50; i++) _factory.Source(vault.Root).Raise($"_audio/p{i}.mp3", VaultChangeKind.Upserted);
        await Task.Delay(500);
        gated.Release();
        await WaitForAsync(() => gated.ListCalls >= 2);
        await Task.Delay(300);

        gated.ListCalls.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Events_outside_audio_do_not_trigger_a_listing()
    {
        var vault = NewVault("_audio/a.mp3");
        var gated = _factory.Gate(vault.Root);
        gated.Release();
        await _host.OpenAsync(vault.Root);
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);
        await WaitForAsync(() => service.Current.Tracks.Count == 1);

        _factory.Source(vault.Root).Raise("Notas/x.md", VaultChangeKind.Upserted);
        await Task.Delay(300);

        gated.ListCalls.Should().Be(1);
    }

    [Fact]
    public async Task Saving_scenes_or_favorites_does_not_trigger_a_listing()
    {
        var vault = NewVault("_audio/a.mp3");
        var gated = _factory.Gate(vault.Root);
        gated.Release();
        await _host.OpenAsync(vault.Root);
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);
        await WaitForAsync(() => service.Current.Tracks.Count == 1);

        _factory.Source(vault.Root).Raise("_audio/_dm/scenes/taberna.json", VaultChangeKind.Upserted);
        _factory.Source(vault.Root).Raise("_audio/_dm/favorites.json", VaultChangeKind.Upserted);
        await Task.Delay(300);

        gated.ListCalls.Should().Be(1);
    }

    [Fact]
    public async Task Switching_vault_during_a_rebuild_ends_with_the_new_vault_library()
    {
        var first = NewVault("_audio/a.mp3");
        var second = NewVault("_audio/b.mp3");
        var gated = _factory.Gate(first.Root);
        await _host.OpenAsync(first.Root);
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);
        await WaitForAsync(() => gated.ListCalls == 1);

        await _host.OpenAsync(second.Root);
        gated.Release();

        await WaitForAsync(() => service.Current.Tracks.Select(track => track.Path).SequenceEqual(["b.mp3"]));
    }

    private sealed class GatedFactory : IVaultServiceFactory
    {
        private readonly Dictionary<string, GatedStorage> _storages = [];
        private readonly Dictionary<string, ManualSource> _sources = [];

        public GatedStorage Gate(string root) => _storages[root] = new GatedStorage(new FileSystemVaultStorage(root), blocked: true);

        public ManualSource Source(string root) => _sources[root];

        public VaultService Create(string rootPath)
        {
            var storage = _storages.TryGetValue(rootPath, out var gated) ? gated : new GatedStorage(new FileSystemVaultStorage(rootPath), blocked: false);
            var source = _sources[rootPath] = new ManualSource();
            return new VaultService(storage, source, NullLogger<VaultService>.Instance);
        }
    }

    private sealed class ManualSource : IVaultChangeSource
    {
        public event EventHandler<VaultChange>? Changed;
        public void Start() { }
        public void Raise(string path, VaultChangeKind kind) => Changed?.Invoke(this, new VaultChange(path, kind));
        public void Dispose() { }
    }

    private sealed class GatedStorage(IVaultStorage inner, bool blocked) : IVaultStorage
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _listCalls;

        public int ListCalls => Volatile.Read(ref _listCalls);

        public void Release() => _gate.TrySetResult();

        public async Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _listCalls);
            if (blocked && call == 1) await _gate.Task;
            return await inner.ListFilesAsync(f, ct);
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
