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
        _provider = new PortraitUrlProvider(_host, _logger);
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
    public async Task Small_png_becomes_a_data_url()
    {
        await OpenVaultAsync(SmallPng);
        var result = await _provider.GetAsync(Portrait);
        (result.Status, result.DataUrl).Should().Be((PortraitStatus.Ok, "data:image/png;base64," + Convert.ToBase64String(SmallPng)));
    }

    [Fact]
    public async Task Oversized_file_is_too_large_and_its_bytes_are_never_read()
    {
        await OpenVaultAsync(new byte[512 * 1024 + 1]);
        var result = await _provider.GetAsync(Portrait);
        (result.Status, result.DataUrl, _factory.ReadCount).Should().Be((PortraitStatus.TooLarge, null, 0));
    }

    [Fact]
    public async Task File_of_exactly_the_limit_is_accepted()
    {
        await OpenVaultAsync(new byte[512 * 1024]);
        (await _provider.GetAsync(Portrait)).Status.Should().Be(PortraitStatus.Ok);
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
    public async Task A_rejected_path_is_logged_as_a_warning_and_returns_missing()
    {
        await OpenVaultAsync(SmallPng);
        // Una ruta fuera del vault hace que el almacenamiento lance ArgumentException.
        var result = await _provider.GetAsync("../fuera.png");

        result.Status.Should().Be(PortraitStatus.Missing);
        _logger.Warnings.Should().Be(1);
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

    [Fact]
    public async Task Second_call_without_changes_does_not_reread()
    {
        await OpenVaultAsync(SmallPng);
        await _provider.GetAsync(Portrait);
        await _provider.GetAsync(Portrait);
        _factory.ReadCount.Should().Be(1);
    }

    [Fact]
    public async Task New_modification_time_rereads()
    {
        var vault = await OpenVaultAsync(SmallPng);
        await _provider.GetAsync(Portrait);
        byte[] changed = [0x89, 0x50, 9, 9];
        WritePortrait(vault, changed);
        File.SetLastWriteTimeUtc(vault.PathOf(Portrait), DateTime.UtcNow.AddMinutes(5));

        var result = await _provider.GetAsync(Portrait);
        (result.DataUrl, _factory.ReadCount).Should().Be(("data:image/png;base64," + Convert.ToBase64String(changed), 2));
    }

    [Fact]
    public async Task Switching_vaults_never_returns_the_other_vaults_image()
    {
        byte[] other = [0x89, 0x50, 7, 7, 7];
        await OpenVaultAsync(SmallPng);
        var first = await _provider.GetAsync(Portrait);
        var second = await OpenVaultAsync(other);
        // Mismo mtime en ambos vaults: solo la raíz distingue las entradas de la caché.
        File.SetLastWriteTimeUtc(second.PathOf(Portrait), File.GetLastWriteTimeUtc(_vaults[0].PathOf(Portrait)));
        _ = first;

        var result = await _provider.GetAsync(Portrait);
        result.DataUrl.Should().Be("data:image/png;base64," + Convert.ToBase64String(other));
    }

    [Theory]
    [InlineData("a.jpg", "image/jpeg")]
    [InlineData("a.JPEG", "image/jpeg")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.gif", "image/gif")]
    public async Task Mime_type_follows_the_extension(string name, string mime)
    {
        var vault = await OpenVaultAsync(null);
        File.WriteAllBytes(vault.PathOf(name), SmallPng);
        (await _provider.GetAsync(name)).DataUrl.Should().StartWith($"data:{mime};base64,");
    }

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
