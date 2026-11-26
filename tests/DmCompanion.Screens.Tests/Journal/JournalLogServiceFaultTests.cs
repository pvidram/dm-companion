using DmCompanion.Core.Journal;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class JournalLogServiceFaultTests : IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly JournalFaultyFactory _factory = new();
    private readonly VaultHost _host;
    private readonly JournalIndex _index;
    private readonly JournalLogService _service;

    public JournalLogServiceFaultTests()
    {
        _host = new VaultHost(_factory, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        _index = new JournalIndex(_host, new VaultFileUrls(_host), NullLogger<JournalIndex>.Instance);
        _service = new JournalLogService(_host, _index, TimeProvider.System, NullLogger<JournalLogService>.Instance);
    }

    Task IAsyncLifetime.InitializeAsync() => _host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        _index.Dispose();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    [Fact]
    public async Task A_generated_note_that_changes_while_generating_is_reported_and_not_overwritten()
    {
        const string old = "---\ntype: journal-log\ngenerated: true\n---\nVieja.\n";
        File.WriteAllText(_vault.PathOf(JournalPaths.LogPath), old);
        _factory.Storage!.FailNoteWrites = true;

        var result = await _service.GenerateAsync();

        (result.Status, File.ReadAllText(_vault.PathOf(JournalPaths.LogPath))).Should().Be((JournalLogStatus.ModifiedOnDisk, old));
    }

    [Fact]
    public async Task A_note_that_appears_while_generating_is_reported_and_not_created()
    {
        _factory.Storage!.FailNoteWrites = true;

        var result = await _service.GenerateAsync();

        (result.Status, File.Exists(_vault.PathOf(JournalPaths.LogPath))).Should().Be((JournalLogStatus.ModifiedOnDisk, false));
    }
}
