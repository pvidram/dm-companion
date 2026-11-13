using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

/// <summary>Fallos de IO a mitad de una operación: ninguna debe dejar el vault a medias.</summary>
public sealed class MapLibraryFailureTests : IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly FaultyFactory _factory = new();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly MapLibrary _library;

    public MapLibraryFailureTests()
    {
        _host = new VaultHost(_factory, _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        _library = new MapLibrary(_host, _session);
    }

    public Task InitializeAsync() => _host.OpenAsync(_vault.Root);

    public async Task DisposeAsync()
    {
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private bool Exists(string path) => File.Exists(_vault.PathOf(path));

    private async Task SeedAsync()
    {
        await _library.ImportAsync("Faro", ".png", new MemoryStream([1, 2, 3]));
        File.WriteAllText(_vault.PathOf("Mapas/Faro.state.json"), "{\"version\":1,\"tokens\":[],\"strokes\":[]}");
    }

    private string Layout() => string.Join(",", new[] { "Faro.md", "Faro.state.json", "Torre.md", "Torre.state.json", "Faro.png" }
        .Where(name => Exists("Mapas/" + name)));

    [Fact]
    public async Task Import_deletes_the_image_when_writing_the_note_throws()
    {
        _factory.Fail = (op, path) => op == "write" && path == "Mapas/Faro.md";

        var result = await _library.ImportAsync("Faro", ".png", new MemoryStream([1, 2, 3]));

        (result.Status, Exists("Mapas/Faro.png")).Should().Be((MapOpStatus.IoError, false));
    }

    [Fact]
    public async Task Rename_undoes_the_note_move_when_moving_the_state_throws()
    {
        await SeedAsync();
        _factory.Fail = (op, path) => op == "move" && path == "Mapas/Faro.state.json";

        var result = await _library.RenameAsync("Mapas/Faro.md", "Torre");

        (result, Layout()).Should().Be((new MapOpResult(MapOpStatus.IoError, "Mapas/Faro.md"), "Faro.md,Faro.state.json,Faro.png"));
    }

    [Fact]
    public async Task Rename_undoes_both_moves_when_writing_the_new_name_throws()
    {
        await SeedAsync();
        _factory.Fail = (op, path) => op == "write" && path == "Mapas/Torre.md";

        var result = await _library.RenameAsync("Mapas/Faro.md", "Torre");

        (result, Layout()).Should().Be((new MapOpResult(MapOpStatus.IoError, "Mapas/Faro.md"), "Faro.md,Faro.state.json,Faro.png"));
    }

    [Fact]
    public async Task Rename_of_the_open_map_reopens_the_old_path_after_a_failure()
    {
        await SeedAsync();
        await _session.OpenAsync("Mapas/Faro.md");
        _factory.Fail = (op, path) => op == "write" && path == "Mapas/Torre.md";

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        _session.Current?.NotePath.Should().Be("Mapas/Faro.md");
    }

    [Fact]
    public async Task Rename_reports_the_real_path_and_reopens_it_when_the_undo_also_fails()
    {
        await SeedAsync();
        await _session.OpenAsync("Mapas/Faro.md");
        _factory.Fail = (op, path) => (op == "write" && path == "Mapas/Torre.md") || (op == "move" && path == "Mapas/Torre.md");

        var result = await _library.RenameAsync("Mapas/Faro.md", "Torre");

        (result, _session.Current?.NotePath).Should().Be((new MapOpResult(MapOpStatus.IoError, "Mapas/Torre.md"), "Mapas/Torre.md"));
    }

    private sealed class FaultyFactory : IVaultServiceFactory
    {
        /// <summary>(operación "write" o "move", ruta de origen) para la que se lanza IOException.</summary>
        public volatile Func<string, string, bool> Fail = (_, _) => false;

        public VaultService Create(string rootPath) => new(
            new FaultyStorage(new FileSystemVaultStorage(rootPath), this),
            new FileSystemVaultWatcher(rootPath),
            NullLogger<VaultService>.Instance);
    }

    private sealed class FaultyStorage(IVaultStorage inner, FaultyFactory owner) : IVaultStorage
    {
        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public Task<VaultFileContent> ReadAsync(string p, CancellationToken ct = default) => inner.ReadAsync(p, ct);
        public Task<byte[]> ReadBytesAsync(string p, CancellationToken ct = default) => inner.ReadBytesAsync(p, ct);
        public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default) => inner.ListFilesAsync(f, ct);
        public Task<Stream> OpenReadAsync(string p, CancellationToken ct = default) => inner.OpenReadAsync(p, ct);
        public Task<VaultFileInfo?> StatAsync(string p, CancellationToken ct = default) => inner.StatAsync(p, ct);
        public Task<WriteResult> WriteBytesAsync(string p, Stream c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteBytesAsync(p, c, pre, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string p, CancellationToken ct = default) => inner.DeleteAsync(p, ct);

        public Task<WriteResult> WriteAsync(string p, string c, WritePrecondition pre, CancellationToken ct = default)
        {
            if (owner.Fail("write", p)) throw new IOException("fallo simulado al escribir");
            return inner.WriteAsync(p, c, pre, ct);
        }

        public Task<bool> MoveAsync(string from, string to, CancellationToken ct = default)
        {
            if (owner.Fail("move", from)) throw new IOException("fallo simulado al mover");
            return inner.MoveAsync(from, to, ct);
        }
    }
}
