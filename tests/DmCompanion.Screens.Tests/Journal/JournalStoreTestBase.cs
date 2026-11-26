using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

/// <summary>El almacén del diario sobre una copia del vault de demo (Lyra y Torvald), con el reloj parado el 16/11/2026 a las 21:34 (+01:00).</summary>
public abstract class JournalStoreTestBase : IAsyncLifetime
{
    protected const string Lyra = "Personajes/Lyra.md";
    protected const string Diary = "Personajes/Lyra/Diario";

    private readonly TempVault _vault = TempVault.FromDemo();

    protected JournalStoreTestBase()
    {
        Clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("mas-uno", TimeSpan.FromHours(1), "mas-uno", "mas-uno"));
        Host = new VaultHost(Factory, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        Store = new JournalStore(Host, Clock, NullLogger<JournalStore>.Instance);
    }

    private protected JournalFaultyFactory Factory { get; } = new();
    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 16, 20, 34, 0, TimeSpan.Zero));
    protected VaultHost Host { get; }
    protected JournalStore Store { get; }

    Task IAsyncLifetime.InitializeAsync() => Host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Host.DisposeAsync();
        _vault.Dispose();
    }

    protected string PathOf(string relativePath) => _vault.PathOf(relativePath);

    protected bool Exists(string relativePath) => File.Exists(PathOf(relativePath));

    /// <summary>Nombres de los ficheros de una carpeta del vault, separados por «|» y en orden ordinal.</summary>
    protected string Names(string folder) =>
        Directory.Exists(PathOf(folder))
            ? string.Join("|", Directory.GetFiles(PathOf(folder)).Select(Path.GetFileName).Order(StringComparer.Ordinal))
            : string.Empty;

    protected static JournalImageSource Image(string fileName, params byte[] bytes) =>
        new(fileName, () => Task.FromResult<Stream>(new MemoryStream(bytes.Length == 0 ? [1, 2, 3] : bytes)));

    protected Task<JournalResult> CreateAsync(string? title, params string[] imageNames) =>
        Store.CreateAsync(new JournalCreate(Lyra, title, null, null, [.. imageNames.Select(name => Image(name))]));

    protected async Task<DateTime> StampAsync(string entryPath) => (await Host.Current!.StatAsync(entryPath))!.LastWriteTimeUtc;
}

/// <summary>Storage real que puede negarse a escribir notas, para probar que el almacén deshace lo que hizo.</summary>
internal sealed class JournalFaultyStorage(IVaultStorage inner) : IVaultStorage
{
    public volatile bool FailNoteWrites;
    public volatile bool ThrowOnNoteWrites;
    public volatile string? LockedNote;

    public string RootPath => inner.RootPath;

    public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);

    public Task<VaultFileContent> ReadAsync(string relativePath, CancellationToken ct = default) =>
        LockedNote is { } locked && VaultPaths.Normalize(relativePath).Equals(locked, StringComparison.OrdinalIgnoreCase)
            ? throw new IOException("Bloqueada por otro proceso.")
            : inner.ReadAsync(relativePath, ct);

    public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default) => inner.ReadBytesAsync(relativePath, ct);

    public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> predicate, CancellationToken ct = default) => inner.ListFilesAsync(predicate, ct);

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) => inner.OpenReadAsync(relativePath, ct);

    public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default) => inner.StatAsync(relativePath, ct);

    public Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default) =>
        ThrowOnNoteWrites && VaultPaths.IsMarkdown(relativePath)
            ? throw new InvalidOperationException("Fallo inesperado del almacenamiento.")
            : FailNoteWrites && VaultPaths.IsMarkdown(relativePath)
            ? Task.FromResult(new WriteResult(WriteStatus.ModifiedOnDisk, DateTime.UtcNow))
            : inner.WriteAsync(relativePath, content, precondition, ct);

    public Task<WriteResult> WriteBytesAsync(string relativePath, Stream content, WritePrecondition precondition, CancellationToken ct = default) =>
        inner.WriteBytesAsync(relativePath, content, precondition, ct);

    public Task<bool> MoveAsync(string fromRelativePath, string toRelativePath, CancellationToken ct = default) =>
        inner.MoveAsync(fromRelativePath, toRelativePath, ct);

    public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);

    public Task DeleteAsync(string relativePath, CancellationToken ct = default) => inner.DeleteAsync(relativePath, ct);
}

internal sealed class JournalFaultyFactory : IVaultServiceFactory
{
    public JournalFaultyStorage? Storage { get; private set; }

    public VaultService Create(string rootPath)
    {
        Storage = new JournalFaultyStorage(new FileSystemVaultStorage(rootPath));
        return new VaultService(Storage, new FileSystemVaultWatcher(rootPath), NullLogger<VaultService>.Instance);
    }
}
