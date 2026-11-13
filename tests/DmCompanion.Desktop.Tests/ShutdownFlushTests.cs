using DmCompanion.Core.Maps;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Desktop.Tests;

public sealed class ShutdownFlushTests : IAsyncLifetime
{
    private const string MapNote = "Mapas/cripta.md";

    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;

    public ShutdownFlushTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    [Fact]
    public async Task FlushBlocking_writes_the_pending_map_state_before_returning()
    {
        Directory.CreateDirectory(_vault.PathOf("Mapas"));
        File.WriteAllText(_vault.PathOf(MapNote), "---\ntype: map\nname: Cripta\ncanvas:\n  width: 1000\n  height: 800\n---\n");
        await _host.OpenAsync(_vault.Root);
        await _session.OpenAsync(MapNote);
        _session.Mutate(editor => editor.AddToken(new TokenDraft("Goblin", null, TokenSide.Enemy, 1, null, null), 35, 35) is not null);
        var stateFile = _vault.PathOf(MapPaths.StatePathFor(MapNote));

        await Task.Run(() => ShutdownFlush.FlushBlocking(_session, TimeSpan.FromSeconds(4), NullLogger.Instance));

        File.Exists(stateFile).Should().BeTrue();
    }
}
