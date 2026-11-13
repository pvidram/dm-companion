using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class PortraitUrlProviderTests : IAsyncLifetime
{
    private const string Portrait = "_assets/lyra.png";
    private static readonly byte[] SmallPng = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private readonly List<TempVault> _vaults = [];
    private readonly CountingFactory _factory = new();
    private readonly VaultHost _host;
    private readonly PortraitUrlProvider _provider;
    private readonly CapturingLogger<PortraitUrlProvider> _logger = new();

    public PortraitUrlProviderTests()
    {
        _host = new VaultHost(_factory, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        _provider = new PortraitUrlProvider(_host, new VaultFileUrls(_host), _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        foreach (var vault in _vaults) vault.Dispose();
    }

    private async Task<TempVault> OpenVaultAsync(byte[]? portraitBytes)
    {
        var vault = TempVault.FromDemo();
        _vaults.Add(vault);
        if (portraitBytes is not null) WritePortrait(vault, portraitBytes);
        (await _host.OpenAsync(vault.Root)).Should().BeTrue();
        return vault;
    }

    private static void WritePortrait(TempVault vault, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(vault.PathOf(Portrait))!);
        File.WriteAllBytes(vault.PathOf(Portrait), bytes);
    }

    [Fact]
    public async Task No_path_means_no_portrait()
    {
        await OpenVaultAsync(null);
        (await _provider.GetAsync(null)).Should().Be(new PortraitResult(null, PortraitStatus.None));
    }

    [Fact]
    public async Task Png_becomes_a_virtual_vault_url_with_the_modification_stamp()
    {
        var vault = await OpenVaultAsync(SmallPng);
        var result = await _provider.GetAsync(Portrait);
        var stamp = File.GetLastWriteTimeUtc(vault.PathOf(Portrait)).Ticks;
        (result.Status, result.Url).Should().Be((PortraitStatus.Ok, $"/_vault/_assets/lyra.png?v={stamp}"));
        _factory.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task A_two_megabyte_portrait_is_ok_and_is_not_inlined()
    {
        await OpenVaultAsync(new byte[2 * 1024 * 1024]);
        var result = await _provider.GetAsync(Portrait);
        result.Status.Should().Be(PortraitStatus.Ok);
        result.Url.Should().StartWith("/_vault/").And.NotStartWith("data:");
    }

    [Fact]
    public async Task Modification_time_changes_the_url()
    {
        var vault = await OpenVaultAsync(SmallPng);
        var first = await _provider.GetAsync(Portrait);
        File.SetLastWriteTimeUtc(vault.PathOf(Portrait), DateTime.UtcNow.AddMinutes(5));
        var second = await _provider.GetAsync(Portrait);
        second.Url.Should().NotBe(first.Url);
    }

    [Fact]
    public async Task Leading_slash_is_normalized_instead_of_being_missing()
    {
        await OpenVaultAsync(SmallPng);
        var result = await _provider.GetAsync("/" + Portrait);
        (result.Status, result.Url).Should().Match<(PortraitStatus, string?)>(t => t.Item1 == PortraitStatus.Ok && t.Item2!.StartsWith("/_vault/_assets/lyra.png"));
    }

    [Fact]
    public async Task Absent_file_is_missing()
    {
        await OpenVaultAsync(null);
        (await _provider.GetAsync(Portrait)).Status.Should().Be(PortraitStatus.Missing);
    }

    [Fact]
    public async Task Path_outside_the_vault_is_missing()
    {
        await OpenVaultAsync(null);
        (await _provider.GetAsync("../fuera.png")).Status.Should().Be(PortraitStatus.Missing);
    }

    [Fact]
    public async Task Unknown_extension_is_missing()
    {
        var vault = await OpenVaultAsync(null);
        Directory.CreateDirectory(vault.PathOf("_assets"));
        File.WriteAllBytes(vault.PathOf("_assets/retrato.bmp"), SmallPng);
        (await _provider.GetAsync("_assets/retrato.bmp")).Status.Should().Be(PortraitStatus.Missing);
    }

    [Fact]
    public async Task Without_an_open_vault_it_is_missing() =>
        (await _provider.GetAsync(Portrait)).Status.Should().Be(PortraitStatus.Missing);

    private sealed class CountingFactory : IVaultServiceFactory
    {
        private int _reads;
        public int ReadCount => _reads;

        public VaultService Create(string rootPath) => new(
            new CountingStorage(new FileSystemVaultStorage(rootPath), () => Interlocked.Increment(ref _reads)),
            new NoChanges(),
            NullLogger<VaultService>.Instance);
    }

    private sealed class NoChanges : IVaultChangeSource
    {
        public event EventHandler<VaultChange>? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() { }
    }

    private sealed class CountingStorage(IVaultStorage inner, Action onRead) : IVaultStorage
    {
        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public Task<VaultFileContent> ReadAsync(string relativePath, CancellationToken ct = default) => inner.ReadAsync(relativePath, ct);
        public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default)
        {
            onRead();
            return inner.ReadBytesAsync(relativePath, ct);
        }
        public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default) => inner.StatAsync(relativePath, ct);
        public Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default) =>
            inner.WriteAsync(relativePath, content, precondition, ct);
        public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> predicate, CancellationToken ct = default) => inner.ListFilesAsync(predicate, ct);
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) => inner.OpenReadAsync(relativePath, ct);
        public Task<WriteResult> WriteBytesAsync(string relativePath, Stream content, WritePrecondition precondition, CancellationToken ct = default) =>
            inner.WriteBytesAsync(relativePath, content, precondition, ct);
        public Task<bool> MoveAsync(string fromRelativePath, string toRelativePath, CancellationToken ct = default) => inner.MoveAsync(fromRelativePath, toRelativePath, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => inner.DeleteAsync(relativePath, ct);
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public int Warnings { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning) Warnings++;
        }
    }
}
