using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

public sealed class MapSessionTests : IAsyncLifetime
{
    private const string MapNote = "Mapas/cripta.md";
    private const string OtherNote = "Mapas/torre.md";
    private const string StatePath = "Mapas/cripta.state.json";
    private const string OtherStatePath = "Mapas/torre.state.json";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();
    private readonly TempVault _second = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly FakeTimeProvider _time = new();
    private readonly CountingFactory _factory = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;

    public MapSessionTests()
    {
        _host = new VaultHost(_factory, _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
    }

    public async Task InitializeAsync()
    {
        WriteNote(_vault, MapNote, "Cripta");
        WriteNote(_vault, OtherNote, "Torre");
        await _host.OpenAsync(_vault.Root);
    }

    public async Task DisposeAsync()
    {
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
        _second.Dispose();
    }

    [Fact]
    public async Task ReportImageMissing_sets_ImageNotice_without_touching_Notice_and_raises_Changed()
    {
        await _session.OpenAsync(MapNote);
        var raised = 0;
        _session.Changed += (_, _) => raised++;

        _session.ReportImageMissing();

        (_session.ImageNotice, _session.Notice, raised).Should().Be(("No se encuentra la imagen del mapa", null, 1));
    }

    [Fact]
    public async Task ImageNotice_is_cleared_when_another_map_is_opened()
    {
        await _session.OpenAsync(MapNote);
        _session.ReportImageMissing();

        await _session.OpenAsync(OtherNote);

        _session.ImageNotice.Should().BeNull();
    }

    [Fact]
    public async Task SetImageSize_ignores_a_note_that_is_not_the_open_map()
    {
        await _session.OpenAsync(MapNote);

        _session.SetImageSize(OtherNote, 4000, 3000);

        (_session.Current!.Width, _session.Current.Height).Should().NotBe((4000, 3000));
    }

    [Fact]
    public async Task SetImageSize_applies_for_the_open_map_and_ignores_unchanged_sizes()
    {
        await _session.OpenAsync(MapNote);
        _session.SetImageSize(MapNote, 4000, 3000);
        var editor = _session.Editor;
        var raised = 0;
        _session.Changed += (_, _) => raised++;

        _session.SetImageSize(MapNote, 4000, 3000);

        (_session.Current!.Width, _session.Current.Height, raised, _session.Editor).Should().Be((4000, 3000, 0, editor));
    }

    private static void WriteNote(TempVault vault, string path, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(vault.PathOf(path))!);
        File.WriteAllText(vault.PathOf(path), $"---\ntype: map\nname: {name}\n---\n");
    }

    private static MapState StateWithToken(double x = 10, double y = 10) =>
        MapState.Empty with { Tokens = [new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, x, y, null, null)] };

    private void SeedState(string path, MapState state) =>
        File.WriteAllText(_vault.PathOf(path), MapStateSerializer.Write(state));

    private string ReadFile(string path) => File.ReadAllText(_vault.PathOf(path));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("La condición no se cumplió a tiempo.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Open_without_state_file_gives_empty_state_and_writes_nothing()
    {
        await _session.OpenAsync(MapNote);

        (_session.Editor!.State, File.Exists(_vault.PathOf(StatePath)), _session.IsReadOnly)
            .Should().Be((MapState.Empty, false, false));
    }

    [Fact]
    public async Task Open_with_invalid_json_is_read_only_and_never_changes_the_file()
    {
        File.WriteAllText(_vault.PathOf(StatePath), "{ esto no es json");
        await _session.OpenAsync(MapNote);

        _session.Mutate(e => e.AddToken(new TokenDraft("Orco", null, TokenSide.Enemy, 1, null, null), 5, 5) is not null);
        await _session.FlushAsync();

        (_session.IsReadOnly, _session.Notice, ReadFile(StatePath))
            .Should().Match<(bool, string?, string)>(t => t.Item1 && t.Item2 != null && t.Item3 == "{ esto no es json");
    }

    [Fact]
    public async Task Open_with_future_version_is_read_only()
    {
        File.WriteAllText(_vault.PathOf(StatePath), "{\"version\":2,\"tokens\":[],\"strokes\":[]}");

        await _session.OpenAsync(MapNote);

        _session.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task Mutate_does_not_write_before_the_delay_and_writes_after_it()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);

        _session.Mutate(e => e.MoveToken("t1", 50, 60));
        _time.Advance(MapSession.SaveDelay - TimeSpan.FromMilliseconds(1));
        ReadFile(StatePath).Should().Contain("\"x\":10");

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => ReadFile(StatePath).Contains("\"x\":50"));
    }

    [Fact]
    public async Task Two_mutations_within_the_delay_produce_a_single_write_with_the_last_state()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _factory.Writes.Clear();

        _session.Mutate(e => e.MoveToken("t1", 20, 20));
        _time.Advance(TimeSpan.FromMilliseconds(500));
        _session.Mutate(e => e.MoveToken("t1", 30, 30));
        _time.Advance(MapSession.SaveDelay);
        await WaitUntilAsync(() => ReadFile(StatePath).Contains("\"x\":30"));
        await _session.FlushAsync();

        _factory.Writes.Should().ContainSingle();
    }

    [Fact]
    public async Task Flush_writes_immediately_without_waiting_for_the_delay()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);

        _session.Mutate(e => e.MoveToken("t1", 70, 80));
        await _session.FlushAsync();

        ReadFile(StatePath).Should().Contain("\"x\":70");
    }

    [Fact]
    public async Task External_change_without_local_changes_reloads_and_raises_Changed()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        var raised = 0;
        _session.Changed += (_, _) => Interlocked.Increment(ref raised);

        SeedState(StatePath, StateWithToken(x: 99));
        File.SetLastWriteTimeUtc(_vault.PathOf(StatePath), DateTime.UtcNow.AddMinutes(1));

        // Changed se notifica tras soltar el gate, es decir, después de cambiar el Editor: se espera al evento, no solo al estado.
        await WaitUntilAsync(() => Volatile.Read(ref raised) > 0);
        _session.Editor!.State.Tokens[0].X.Should().Be(99);
    }

    [Fact]
    public async Task Repeated_external_events_are_harmless_and_own_writes_are_ignored()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 40, 40));
        await _session.FlushAsync();
        var raised = 0;
        _session.Changed += (_, _) => Interlocked.Increment(ref raised);

        await Task.Delay(500);

        (raised, _session.Editor!.State.Tokens[0].X).Should().Be((0, 40));
    }

    [Fact]
    public async Task External_change_with_unsaved_local_changes_is_overwritten_on_next_save_with_a_notice()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 11, 11));

        SeedState(StatePath, StateWithToken(x: 99));
        File.SetLastWriteTimeUtc(_vault.PathOf(StatePath), DateTime.UtcNow.AddMinutes(1));
        await WaitUntilAsync(() => _session.Notice is not null);
        await _session.FlushAsync();

        (ReadFile(StatePath).Contains("\"x\":11"), _session.Notice).Should().Match<(bool, string?)>(t => t.Item1 && t.Item2!.Contains("sobrescrib"));
    }

    [Fact]
    public async Task Opening_another_map_saves_the_previous_one_in_its_own_state_file()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 77, 77));

        await _session.OpenAsync(OtherNote);

        (ReadFile(StatePath).Contains("\"x\":77"), File.Exists(_vault.PathOf(OtherStatePath)), _session.Current!.NotePath)
            .Should().Be((true, false, OtherNote));
    }

    [Fact]
    public async Task Switching_vault_saves_the_pending_changes_first()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 88, 88));

        await _host.OpenAsync(_second.Root);

        (ReadFile(StatePath).Contains("\"x\":88"), _session.Current, File.Exists(_second.PathOf(StatePath)))
            .Should().Be((true, null, false));
    }

    [Fact]
    public async Task Unexpected_exception_while_saving_sets_a_notice_and_keeps_the_map_dirty()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 66, 66));
        _factory.CrashWrites = true;

        await _session.FlushAsync();
        var noticeWhileFailing = _session.Notice;
        _factory.CrashWrites = false;
        await _session.FlushAsync();

        (noticeWhileFailing is not null, ReadFile(StatePath).Contains("\"x\":66")).Should().Be((true, true));
    }

    [Fact]
    public async Task Unexpected_exception_while_saving_aborts_the_vault_switch()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 66, 66));
        _factory.CrashWrites = true;

        var switched = await _host.OpenAsync(_second.Root);

        (switched, _host.Current!.RootPath, _session.Current?.NotePath, _session.Notice is not null)
            .Should().Be((false, _vault.Root, MapNote, true));
    }

    [Fact]
    public async Task Throwing_Changed_subscriber_does_not_break_open_or_save()
    {
        SeedState(StatePath, StateWithToken());
        _session.Changed += (_, _) => throw new InvalidOperationException("suscriptor roto");

        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 33, 33));
        await _session.FlushAsync();

        (_session.Current?.NotePath, ReadFile(StatePath).Contains("\"x\":33")).Should().Be((MapNote, true));
    }

    [Fact]
    public async Task Throwing_Changed_subscriber_does_not_allow_a_switch_after_a_failed_save()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 66, 66));
        _session.Changed += (_, _) => throw new InvalidOperationException("suscriptor roto");
        _factory.FailWrites = true;

        var switched = await _host.OpenAsync(_second.Root);

        (switched, _host.Current!.RootPath, _session.Current?.NotePath).Should().Be((false, _vault.Root, MapNote));
    }

    [Fact]
    public async Task Mutation_during_a_slow_write_still_leaves_the_final_state_on_disk()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _factory.WriteDelay = TimeSpan.FromMilliseconds(300);

        _session.Mutate(e => e.MoveToken("t1", 1, 1));
        var first = _session.FlushAsync();
        await WaitUntilAsync(() => _factory.WritesStarted > 0);
        _session.Mutate(e => e.MoveToken("t1", 2, 2));
        await first;
        await _session.FlushAsync();

        ReadFile(StatePath).Should().Contain("\"x\":2");
    }

    [Fact]
    public async Task OpenLast_reopens_the_saved_map_and_missing_note_leaves_Current_null()
    {
        await _session.OpenAsync(MapNote);
        await _session.CloseAsync();
        await _session.OpenLastAsync();
        var reopened = _session.Current?.NotePath;

        File.Delete(_vault.PathOf(MapNote));
        await _host.OpenAsync(_vault.Root);
        await _session.CloseAsync();
        await _session.OpenLastAsync();

        (reopened, _session.Current).Should().Be((MapNote, null));
    }

    [Fact]
    public async Task Failed_save_keeps_the_current_map_and_its_state_when_opening_another()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 55, 55));
        _factory.FailWrites = true;

        await _session.OpenAsync(OtherNote);

        (_session.Current!.NotePath, _session.Editor!.State.Tokens[0].X, _session.Notice is not null)
            .Should().Be((MapNote, 55, true));
    }

    [Fact]
    public async Task Failed_save_keeps_the_map_in_memory_and_retries_on_a_later_flush()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 56, 56));
        _factory.FailWrites = true;
        await _session.CloseAsync();
        var keptOpen = _session.Current is not null;

        _factory.FailWrites = false;
        await _session.FlushAsync();

        (keptOpen, ReadFile(StatePath).Contains("\"x\":56")).Should().Be((true, true));
    }

    [Fact]
    public async Task Failed_save_aborts_the_vault_switch_and_keeps_the_state_in_memory()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 57, 57));
        _factory.FailWrites = true;

        var switched = await _host.OpenAsync(_second.Root);

        (switched, _host.Current!.RootPath, _host.LastError is not null, _session.Editor!.State.Tokens[0].X)
            .Should().Be((false, _vault.Root, true, 57));
    }

    [Fact]
    public async Task Mutation_during_an_external_reload_is_not_overwritten_by_the_loaded_state()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _factory.ReadDelay = TimeSpan.FromMilliseconds(600);
        var reads = _factory.ReadsStarted;

        SeedState(StatePath, StateWithToken(x: 99));
        File.SetLastWriteTimeUtc(_vault.PathOf(StatePath), DateTime.UtcNow.AddMinutes(1));
        await WaitUntilAsync(() => _factory.ReadsStarted > reads);
        _session.Mutate(e => e.MoveToken("t1", 12, 12));
        await WaitUntilAsync(() => _session.Notice is not null);
        _factory.ReadDelay = TimeSpan.Zero;
        await _session.FlushAsync();

        (ReadFile(StatePath).Contains("\"x\":12"), _session.Editor!.State.Tokens[0].X).Should().Be((true, 12));
    }

    // La carrera real (Mutate entre la comprobación de Dirty y la foto de la revisión) no es reproducible
    // de forma determinista: se fuerza el estado resultante (solo lectura con revisión sin guardar) por reflexión.
    private void ForceReadOnlyDirty()
    {
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(MapSession).GetField("_revision", Flags)!.SetValue(_session, 1);
        typeof(MapSession).GetProperty(nameof(MapSession.IsReadOnly))!.SetValue(_session, true);
    }

    [Fact]
    public async Task Close_and_open_terminate_when_the_map_is_read_only_with_a_pending_revision_and_write_nothing()
    {
        File.WriteAllText(_vault.PathOf(StatePath), "{ esto no es json");
        await _session.OpenAsync(MapNote);
        ForceReadOnlyDirty();

        var open = Task.Run(() => _session.OpenAsync(OtherNote));
        var openDone = await Task.WhenAny(open, Task.Delay(Timeout)) == open;
        ForceReadOnlyDirty();
        var close = Task.Run(() => _session.CloseAsync());
        var closeDone = await Task.WhenAny(close, Task.Delay(Timeout)) == close;

        (openDone, closeDone, ReadFile(StatePath)).Should().Be((true, true, "{ esto no es json"));
    }

    [Fact]
    public async Task DiscardAndClose_after_a_failed_save_closes_the_map_without_writing_and_clears_the_notice()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 58, 58));
        _factory.FailWrites = true;
        await _session.FlushAsync();
        _factory.FailWrites = false;
        _factory.Writes.Clear();

        await _session.DiscardAndCloseAsync();

        (_session.Current, _session.Notice, _session.HasSaveFailure, _factory.Writes.Count, ReadFile(StatePath).Contains("\"x\":10"))
            .Should().Be((null, null, false, 0, true));
    }

    [Fact]
    public async Task Another_map_can_be_opened_after_discarding_a_map_that_could_not_be_saved()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 59, 59));
        _factory.FailWrites = true;
        await _session.FlushAsync();
        await _session.DiscardAndCloseAsync();

        await _session.OpenAsync(OtherNote);

        _session.Current!.NotePath.Should().Be(OtherNote);
    }

    [Fact]
    public async Task HasSaveFailure_is_true_after_a_failed_save()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 60, 60));
        _factory.FailWrites = true;

        await _session.FlushAsync();

        _session.HasSaveFailure.Should().BeTrue();
    }

    // Cuenta y retrasa las escrituras del storage real.
    private sealed class CountingFactory : IVaultServiceFactory
    {
        public List<string> Writes { get; } = [];
        public int WritesStarted;
        public TimeSpan WriteDelay;
        public TimeSpan ReadDelay;
        public int ReadsStarted;
        public volatile bool FailWrites;
        public volatile bool CrashWrites;

        public VaultService Create(string rootPath) => new(
            new CountingStorage(new FileSystemVaultStorage(rootPath), this),
            new FileSystemVaultWatcher(rootPath),
            NullLogger<VaultService>.Instance);
    }

    private sealed class CountingStorage(IVaultStorage inner, CountingFactory owner) : IVaultStorage
    {
        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public async Task<VaultFileContent> ReadAsync(string p, CancellationToken ct = default)
        {
            if (MapPaths.IsStateFile(p))
            {
                Interlocked.Increment(ref owner.ReadsStarted);
                if (owner.ReadDelay > TimeSpan.Zero) await Task.Delay(owner.ReadDelay, ct);
            }
            return await inner.ReadAsync(p, ct);
        }
        public Task<byte[]> ReadBytesAsync(string p, CancellationToken ct = default) => inner.ReadBytesAsync(p, ct);
        public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default) => inner.ListFilesAsync(f, ct);
        public Task<Stream> OpenReadAsync(string p, CancellationToken ct = default) => inner.OpenReadAsync(p, ct);
        public Task<VaultFileInfo?> StatAsync(string p, CancellationToken ct = default) => inner.StatAsync(p, ct);
        public Task<WriteResult> WriteBytesAsync(string p, Stream c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteBytesAsync(p, c, pre, ct);
        public Task<bool> MoveAsync(string a, string b, CancellationToken ct = default) => inner.MoveAsync(a, b, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string p, CancellationToken ct = default) => inner.DeleteAsync(p, ct);

        public async Task<WriteResult> WriteAsync(string p, string c, WritePrecondition pre, CancellationToken ct = default)
        {
            if (!MapPaths.IsStateFile(p)) return await inner.WriteAsync(p, c, pre, ct);
            Interlocked.Increment(ref owner.WritesStarted);
            if (owner.FailWrites) throw new IOException("disco lleno (simulado)");
            if (owner.CrashWrites) throw new InvalidOperationException("fallo inesperado (simulado)");
            if (owner.WriteDelay > TimeSpan.Zero) await Task.Delay(owner.WriteDelay, ct);
            var result = await inner.WriteAsync(p, c, pre, ct);
            lock (owner.Writes) owner.Writes.Add(p);
            return result;
        }
    }
}
