using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
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

    [Fact]
    public async Task DisposeAsync_called_twice_does_not_throw()
    {
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        await session.DisposeAsync();

        var second = async () => await session.DisposeAsync();

        await second.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FlushAsync_after_dispose_does_not_throw_and_does_not_write()
    {
        SeedState(StatePath, StateWithToken());
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        await session.DisposeAsync();
        _factory.Writes.Clear();
        session.Mutate(e => e.MoveToken("t1", 55, 55));

        var flush = async () => await session.FlushAsync();

        await flush.Should().NotThrowAsync();
        _factory.Writes.Should().BeEmpty();
    }

    // Escritor externo no atómico: deja el fichero con ese contenido y ese mtime, sin renombrar.
    private void WriteExternallyInPlace(string path, string content, DateTime mtime)
    {
        File.WriteAllText(_vault.PathOf(path), content);
        File.SetLastWriteTimeUtc(_vault.PathOf(path), mtime);
    }

    // Abre el mapa, escribe `content` en el sitio y espera a que la sesión haya intentado leerlo.
    private async Task OpenAndWriteInPlaceAsync(string content, DateTime mtime)
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        var reads = Volatile.Read(ref _factory.ReadsStarted);
        WriteExternallyInPlace(StatePath, content, mtime);
        await WaitUntilAsync(() => Volatile.Read(ref _factory.ReadsStarted) > reads);
    }

    // Avanza un reintento y espera a que conste que se ha vuelto a leer: el intento anterior ya terminó.
    private async Task AdvanceOneRetryAsync()
    {
        var reads = Volatile.Read(ref _factory.ReadsStarted);
        await WaitUntilAsync(() =>
        {
            _time.Advance(MapSession.ReloadRetryDelay);
            return Volatile.Read(ref _factory.ReadsStarted) > reads;
        });
    }

    // El retardo del reintento corre en el FakeTimeProvider: se avanza hasta que la condición se cumple.
    private async Task PumpRetriesUntilAsync(Func<bool> condition) =>
        await WaitUntilAsync(() =>
        {
            _time.Advance(MapSession.ReloadRetryDelay);
            return condition();
        });

    [Fact]
    public async Task Truncated_external_file_completed_later_with_the_same_mtime_is_reloaded_and_stays_writable()
    {
        var mtime = DateTime.UtcNow.AddMinutes(1);
        await OpenAndWriteInPlaceAsync("", mtime);

        WriteExternallyInPlace(StatePath, MapStateSerializer.Write(StateWithToken(x: 99)), mtime);
        await PumpRetriesUntilAsync(() => _session.Editor!.State.Tokens is [{ X: 99 }]);

        _session.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task Invalid_read_during_an_external_reload_does_not_raise_Changed()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        var raised = 0;
        _session.Changed += (_, _) => Interlocked.Increment(ref raised);
        var reads = Volatile.Read(ref _factory.ReadsStarted);

        WriteExternallyInPlace(StatePath, "", DateTime.UtcNow.AddMinutes(1));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.ReadsStarted) > reads);
        await AdvanceOneRetryAsync();

        Volatile.Read(ref raised).Should().Be(0);
    }

    [Fact]
    public async Task Invalid_read_during_an_external_reload_keeps_the_previous_state_and_stays_writable()
    {
        await OpenAndWriteInPlaceAsync("", DateTime.UtcNow.AddMinutes(1));
        await AdvanceOneRetryAsync();

        (_session.IsReadOnly, _session.Editor!.State.Tokens.Count).Should().Be((false, 1));
    }

    [Fact]
    public async Task External_file_still_invalid_after_the_retries_leaves_the_map_read_only_with_a_notice()
    {
        await OpenAndWriteInPlaceAsync("{ no es json", DateTime.UtcNow.AddMinutes(1));

        await PumpRetriesUntilAsync(() => _session.IsReadOnly);

        _session.Notice.Should().Contain("no es válido");
    }

    // Un escritor a medias da varios intentos de recarga por un solo evento: en `adb logcat` debe verse un solo «cambio externo».
    [Fact]
    public async Task An_external_change_retried_until_it_gives_up_is_logged_once()
    {
        var logs = new CapturingLogger<MapSession>();
        var session = new MapSession(_host, _prefs, _time, logs);
        SeedState(StatePath, StateWithToken());
        await session.OpenAsync(MapNote);
        var reads = Volatile.Read(ref _factory.ReadsStarted);
        var target = _vault.PathOf(StatePath);
        File.WriteAllText(target + ".tmp", "{ no es json");
        File.SetLastWriteTimeUtc(target + ".tmp", DateTime.UtcNow.AddMinutes(1));

        File.Move(target + ".tmp", target, overwrite: true);
        await PumpRetriesUntilAsync(() => session.IsReadOnly);
        await session.DisposeAsync();

        int external;
        lock (logs.Messages) external = logs.Messages.Count(m => m.Contains("cambio externo"));
        (external, Volatile.Read(ref _factory.ReadsStarted) - reads > 1).Should().Be((1, true));
    }

    [Fact]
    public async Task External_file_with_a_newer_version_goes_read_only_without_waiting_for_retries()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        var future = MapStateSerializer.Write(StateWithToken()).Replace("\"version\":2", "\"version\":4");

        WriteExternallyInPlace(StatePath, future, DateTime.UtcNow.AddMinutes(1));
        await WaitUntilAsync(() => _session.IsReadOnly);

        _session.Notice.Should().Contain("más nueva");
    }

    [Fact]
    public async Task Closing_the_map_does_not_reread_on_the_pending_retries()
    {
        await OpenAndWriteInPlaceAsync("", DateTime.UtcNow.AddMinutes(1));
        await _session.CloseAsync();
        var reads = Volatile.Read(ref _factory.ReadsStarted);

        _time.Advance(MapSession.ReloadRetryDelay * (MapSession.ReloadRetries + 1));
        await Task.Delay(100);

        Volatile.Read(ref _factory.ReadsStarted).Should().Be(reads);
    }

    [Fact]
    public async Task A_later_valid_external_reload_is_not_reread_by_the_pending_retries_of_an_earlier_one()
    {
        await OpenAndWriteInPlaceAsync("", DateTime.UtcNow.AddMinutes(1));

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => _session.Editor!.State.Tokens is [{ X: 99 }]);
        var reads = Volatile.Read(ref _factory.ReadsStarted);
        _time.Advance(MapSession.ReloadRetryDelay * (MapSession.ReloadRetries + 1));
        await Task.Delay(200);

        (Volatile.Read(ref _factory.ReadsStarted), _session.IsReadOnly).Should().Be((reads, false));
    }

    [Fact]
    public async Task First_dispose_saves_pending_changes_and_the_second_does_not_write_again()
    {
        SeedState(StatePath, StateWithToken());
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        _factory.Writes.Clear();
        session.Mutate(e => e.MoveToken("t1", 55, 55));

        await session.DisposeAsync();
        var afterFirst = _factory.Writes.Count;
        await session.DisposeAsync();

        (afterFirst, _factory.Writes.Count).Should().Be((1, 1));
    }

    [Fact]
    public async Task External_changes_after_dispose_are_not_read()
    {
        SeedState(StatePath, StateWithToken());
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        await session.DisposeAsync();
        // Sesión testigo: su recarga es la señal positiva de que el watcher ya despachó el cambio.
        await _session.OpenAsync(MapNote);
        var reads = Volatile.Read(ref _factory.ReadsStarted);

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => _session.Editor!.State.Tokens is [{ X: 99 }]);

        Volatile.Read(ref _factory.ReadsStarted).Should().Be(reads + 1);
    }

    [Fact]
    public async Task A_pending_retry_does_not_act_on_another_map_opened_meanwhile()
    {
        await OpenAndWriteInPlaceAsync("", DateTime.UtcNow.AddMinutes(1));
        await AdvanceOneRetryAsync();
        await AdvanceOneRetryAsync();
        // Al reintento de A solo le queda el último intento, que aplicaría el fallo como solo lectura.
        SeedState(OtherStatePath, StateWithToken());
        await _session.OpenAsync(OtherNote);
        var reads = Volatile.Read(ref _factory.ReadsStarted);
        WriteExternallyInPlace(OtherStatePath, "", DateTime.UtcNow.AddMinutes(2));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.ReadsStarted) > reads);

        await AdvanceOneRetryAsync();
        await Task.Delay(100);

        (_session.IsReadOnly, _session.Editor!.State.Tokens.Count).Should().Be((false, 1));
    }

    [Fact]
    public async Task Local_edit_with_a_pending_retry_wins_without_a_notice_and_overwrites_the_file()
    {
        await OpenAndWriteInPlaceAsync("", DateTime.UtcNow.AddMinutes(1));
        _session.Mutate(e => e.MoveToken("t1", 77, 77));

        await PumpRetriesUntilAsync(() => ReadFile(StatePath).Contains("77"));

        (_session.IsReadOnly, _session.Editor!.State.Tokens is [{ X: 77 }], _session.Notice).Should().Be((false, true, null));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task A_failing_read_during_an_external_reload_is_transient_and_recovers(Type failure)
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _factory.ReadFailure = (Exception)Activator.CreateInstance(failure, "lectura fallida (simulada)")!;
        var reads = Volatile.Read(ref _factory.ReadsStarted);

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.ReadsStarted) > reads);
        await AdvanceOneRetryAsync();
        var keptWhileFailing = (_session.IsReadOnly, _session.Editor!.State.Tokens is [{ X: 10 }]);
        _factory.ReadFailure = null;
        await PumpRetriesUntilAsync(() => _session.Editor!.State.Tokens is [{ X: 99 }]);

        (keptWhileFailing, _session.IsReadOnly).Should().Be(((false, true), false));
    }

    [Fact]
    public async Task Final_invalid_read_does_not_remember_the_mtime_so_the_completed_file_with_the_same_mtime_is_reloaded()
    {
        var mtime = DateTime.UtcNow.AddMinutes(1);
        await OpenAndWriteInPlaceAsync("{ no es json", mtime);
        await PumpRetriesUntilAsync(() => _session.IsReadOnly);

        WriteExternallyInPlace(StatePath, MapStateSerializer.Write(StateWithToken(x: 99)), mtime);
        await WaitUntilAsync(() => _session.Editor!.State.Tokens is [{ X: 99 }]);

        _session.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task File_deleted_between_the_two_stats_of_a_reload_keeps_the_previous_state()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _factory.DeleteAfterNextStateStat = _vault.PathOf(StatePath);

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => _factory.DeleteAfterNextStateStat is null);
        await Task.Delay(100);

        (_session.IsReadOnly, _session.Editor!.State.Tokens is [{ X: 10 }]).Should().Be((false, true));
    }

    [Fact]
    public async Task Closing_the_container_after_the_session_is_disposed_does_not_abort_the_vault_switch()
    {
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        await session.DisposeAsync();

        var switched = await _host.OpenAsync(_second.Root);

        switched.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_during_a_slow_open_with_a_late_edit_finishes_and_saves_that_edit()
    {
        SeedState(StatePath, StateWithToken());
        SeedState(OtherStatePath, StateWithToken());
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        _factory.ReadDelay = TimeSpan.FromMilliseconds(500);
        var reads = Volatile.Read(ref _factory.ReadsStarted);
        var opening = session.OpenAsync(OtherNote);
        await WaitUntilAsync(() => Volatile.Read(ref _factory.ReadsStarted) > reads);

        session.Mutate(e => e.MoveToken("t1", 77, 77));
        var disposing = session.DisposeAsync().AsTask();
        var both = Task.WhenAll(opening, disposing);
        var finished = await Task.WhenAny(both, Task.Delay(TimeSpan.FromSeconds(5))) == both;

        (finished, finished && ReadFile(StatePath).Contains("77")).Should().Be((true, true));
    }

    [Fact]
    public async Task Mutate_after_dispose_changes_nothing_and_raises_no_Changed()
    {
        SeedState(StatePath, StateWithToken());
        var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        await session.DisposeAsync();
        var raised = 0;
        session.Changed += (_, _) => raised++;

        session.Mutate(e => e.MoveToken("t1", 55, 55));

        (raised, session.Editor!.State.Tokens is [{ X: 10 }]).Should().Be((0, true));
    }

    // Carrera real: el dispose puede terminar entre la comprobación de IsDisposed y StopTimer, o soltar y desechar el gate
    // mientras el cierre lo tiene tomado. Se repite porque cada iteración solo cae en una ventana a veces.
    [Fact]
    public async Task Closing_while_the_session_is_disposed_in_parallel_never_throws()
    {
        SeedState(StatePath, StateWithToken());
        var failures = new List<string>();
        for (var i = 0; i < 300; i++)
        {
            var session = new MapSession(_host, _prefs, _time, NullLogger<MapSession>.Instance);
            await session.OpenAsync(MapNote);
            var x = i;
            session.Mutate(e => e.MoveToken("t1", x, x));
            using var start = new Barrier(2);
            var closing = Task.Run(async () => { start.SignalAndWait(); await session.CloseAsync(); });
            var disposing = Task.Run(async () => { start.SignalAndWait(); await session.DisposeAsync(); });
            try
            {
                await Task.WhenAll(closing, disposing);
            }
            catch (Exception ex)
            {
                failures.Add($"{ex.GetType().Name} en {ex.StackTrace?.Split('\n')[0].Trim()}");
            }
        }

        failures.Should().BeEmpty();
    }

    // Ventana 1: el dispose termina entero entre la comprobación de IsDisposed del cierre y su StopTimer.
    [Fact]
    public async Task Close_whose_timer_is_disposed_just_after_checking_IsDisposed_does_not_throw()
    {
        var time = new HookedTimeProvider();
        var session = new MapSession(_host, _prefs, time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        session.Mutate(e => e.AddToken(new TokenDraft("Orco", null, TokenSide.Enemy, 1, null, null), 5, 5) is not null);
        await session.FlushAsync();
        time.BeforeNextChange = () => session.DisposeAsync().AsTask().GetAwaiter().GetResult();

        var close = async () => await session.CloseAsync();

        await close.Should().NotThrowAsync();
    }

    // Ventana 2: el cierre espera el gate detrás del guardado final del dispose y lo obtiene justo antes de que se deseche.
    [Fact]
    public async Task Close_that_gets_the_gate_just_before_dispose_releases_it_does_not_throw()
    {
        SeedState(StatePath, StateWithToken());
        var time = new HookedTimeProvider();
        var session = new MapSession(_host, _prefs, time, NullLogger<MapSession>.Instance);
        await session.OpenAsync(MapNote);
        session.Mutate(e => e.MoveToken("t1", 66, 66));
        _factory.WriteDelay = TimeSpan.FromMilliseconds(100);
        Task? disposing = null;
        time.BeforeNextChange = () => disposing = session.DisposeAsync().AsTask();

        var close = async () => await session.CloseAsync();

        await close.Should().NotThrowAsync();
        await disposing!;
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

    // Cambio externo atómico, como el de Syncthing: contenido y mtime finales en un temporal y renombrado.
    // Con WriteAllText + SetLastWriteTime el watcher puede leer el fichero truncado o con el mtime provisional.
    private void ReplaceExternally(string path, MapState state)
    {
        var target = _vault.PathOf(path);
        var temp = target + ".tmp";
        File.WriteAllText(temp, MapStateSerializer.Write(state));
        File.SetLastWriteTimeUtc(temp, DateTime.UtcNow.AddMinutes(1));
        File.Move(temp, target, overwrite: true);
    }

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
        File.WriteAllText(_vault.PathOf(StatePath), "{\"version\":4,\"tokens\":[],\"strokes\":[]}");

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

        ReplaceExternally(StatePath, StateWithToken(x: 99));

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

    // Almacenamiento con mtime distinto entre la escritura y un stat posterior (FUSE de Android, FS de resolución gruesa):
    // el evento del watcher de la propia escritura no debe tomarse por un cambio externo.
    [Fact]
    public async Task Own_write_whose_later_stat_gives_another_mtime_is_not_taken_as_an_external_change()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _factory.WriteResultSkew = TimeSpan.FromMilliseconds(1);
        _factory.WriteDelay = TimeSpan.FromMilliseconds(300);
        var writes = Volatile.Read(ref _factory.WritesStarted);
        _session.Mutate(e => e.MoveToken("t1", 20, 20));
        var flush = _session.FlushAsync();
        await WaitUntilAsync(() => Volatile.Read(ref _factory.WritesStarted) > writes);
        _session.Mutate(e => e.MoveToken("t1", 21, 21));
        var stats = Volatile.Read(ref _factory.StateStats);
        await flush;
        await WaitUntilAsync(() => Volatile.Read(ref _factory.StateStats) > stats);
        _factory.WriteDelay = TimeSpan.Zero;

        await _session.FlushAsync();

        _factory.StatePreconditions.Should().NotContain(WriteMode.Overwrite);
    }

    // Syncthing (u otro proceso) puede tocar solo el mtime del fichero sin cambiar su contenido.
    [Fact]
    public async Task Mtime_only_change_with_the_same_content_and_unsaved_local_changes_is_not_taken_as_an_external_change()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 30, 30));
        var stats = Volatile.Read(ref _factory.StateStats);

        File.SetLastWriteTimeUtc(_vault.PathOf(StatePath), DateTime.UtcNow.AddMinutes(1));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.StateStats) > stats);
        await _session.FlushAsync();

        _factory.StatePreconditions.Should().NotContain(WriteMode.Overwrite);
    }

    // Para confirmar la causa en la tablet con `adb logcat`: tipo de evento, sin rutas ni contenido.
    [Fact]
    public async Task Mtime_only_change_is_logged_without_paths_or_content()
    {
        var logs = new CapturingLogger<MapSession>();
        var session = new MapSession(_host, _prefs, _time, logs);
        SeedState(StatePath, StateWithToken());
        await session.OpenAsync(MapNote);
        var stats = Volatile.Read(ref _factory.StateStats);

        File.SetLastWriteTimeUtc(_vault.PathOf(StatePath), DateTime.UtcNow.AddMinutes(1));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.StateStats) > stats);
        await session.FlushAsync();
        await session.DisposeAsync();

        string[] messages;
        lock (logs.Messages) messages = [.. logs.Messages];
        messages.Should().Contain(m => m.Contains("mismo contenido"))
            .And.NotContain(m => m.Contains("cripta") || m.Contains("Goblin") || m.Contains(_vault.Root));
    }

    [Fact]
    public async Task External_change_with_unsaved_local_changes_is_overwritten_on_next_save_without_a_notice()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 11, 11));
        var stats = Volatile.Read(ref _factory.StateStats);

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.StateStats) > stats);
        await _session.FlushAsync();

        (ReadFile(StatePath).Contains("\"x\":11"), _session.Notice).Should().Be((true, null));
    }

    // Sin esperar al watcher: el guardado encuentra el fichero cambiado (precondición fallida) y lo sobrescribe.
    [Fact]
    public async Task Save_that_finds_the_file_changed_on_disk_overwrites_it_without_a_notice()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 13, 13));

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await _session.FlushAsync();
        await Task.Delay(300);

        (ReadFile(StatePath).Contains("\"x\":13"), _session.Notice).Should().Be((true, null));
    }

    // Guarda con una sesión que registra y devuelve las líneas «Al guardar…»; `beforeWrite` toca el fichero justo antes de
    // la escritura condicionada (con el gate tomado), así que el guardado encuentra el mtime cambiado sin que el watcher se adelante.
    private async Task<(Microsoft.Extensions.Logging.LogLevel Level, string Message)[]> SaveAfterDiskTouchAsync(Action beforeWrite)
    {
        var logs = new CapturingLogger<MapSession>();
        var session = new MapSession(_host, _prefs, _time, logs);
        SeedState(StatePath, StateWithToken());
        await session.OpenAsync(MapNote);
        session.Mutate(e => e.MoveToken("t1", 17, 17));
        _factory.BeforeNextStateWrite = beforeWrite;
        await session.FlushAsync();
        await session.DisposeAsync();
        lock (logs.Messages) return [.. logs.Entries.Where(e => e.Message.StartsWith("Al guardar"))];
    }

    private void TouchStateMtime() => File.SetLastWriteTimeUtc(_vault.PathOf(StatePath), DateTime.UtcNow.AddMinutes(1));

    [Fact]
    public async Task Save_that_finds_only_the_mtime_changed_logs_information_and_not_a_conflict()
    {
        var entries = await SaveAfterDiskTouchAsync(TouchStateMtime);

        entries.Should().ContainSingle().Which.Should().Match<(Microsoft.Extensions.Logging.LogLevel Level, string Message)>(e =>
            e.Level == Microsoft.Extensions.Logging.LogLevel.Information && e.Message.Contains("con el mismo contenido; no es un cambio externo"));
    }

    [Fact]
    public async Task Save_that_finds_only_the_mtime_changed_still_writes_the_local_state()
    {
        await SaveAfterDiskTouchAsync(TouchStateMtime);

        ReadFile(StatePath).Should().Contain("\"x\":17");
    }

    [Fact]
    public async Task Save_that_finds_other_content_on_disk_logs_a_warning_and_overwrites_with_the_local_state()
    {
        var entries = await SaveAfterDiskTouchAsync(() => ReplaceExternally(StatePath, StateWithToken(x: 99)));

        (entries.Select(e => (e.Level, e.Message.Contains("había cambiado en disco"))), ReadFile(StatePath).Contains("\"x\":17")).Should()
            .BeEquivalentTo((new[] { (Microsoft.Extensions.Logging.LogLevel.Warning, true) }, true));
    }

    [Fact]
    public async Task Save_that_cannot_read_the_changed_file_logs_only_the_exception_type_and_overwrites()
    {
        var entries = await SaveAfterDiskTouchAsync(() =>
        {
            TouchStateMtime();
            _factory.ReadFailure = new IOException("texto secreto de la excepción");
        });

        (entries.Single().Message.Contains(nameof(IOException)), entries.Single().Message.Contains("secreto"), ReadFile(StatePath).Contains("\"x\":17"))
            .Should().Be((true, false, true));
    }

    [Fact]
    public async Task Save_diagnostics_have_no_paths_or_content()
    {
        var entries = await SaveAfterDiskTouchAsync(() => ReplaceExternally(StatePath, StateWithToken(x: 99)));

        entries.Should().NotBeEmpty().And.NotContain(e => e.Message.Contains("cripta") || e.Message.Contains("Goblin") || e.Message.Contains(_vault.Root));
    }

    [Fact]
    public async Task HasUnsavedChanges_is_true_after_a_change_and_false_once_saved()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 14, 14));
        var pending = _session.HasUnsavedChanges;

        await _session.FlushAsync();

        (pending, _session.HasUnsavedChanges).Should().Be((true, false));
    }

    [Fact]
    public async Task HasUnsavedChanges_stays_true_while_the_save_fails()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 15, 15));
        _factory.FailWrites = true;

        await _session.FlushAsync();

        _session.HasUnsavedChanges.Should().BeTrue();
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

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => _factory.ReadsStarted > reads);
        _session.Mutate(e => e.MoveToken("t1", 12, 12));
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

    [Fact]
    public async Task DismissNotice_keeps_a_save_failure_notice()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.MoveToken("t1", 60, 60));
        _factory.FailWrites = true;
        await _session.FlushAsync();

        _session.DismissNotice();

        (_session.Notice is not null, _session.HasSaveFailure).Should().Be((true, true));
    }

    [Fact]
    public async Task Repaired_state_shows_a_notice_with_the_number_of_corrections()
    {
        var logs = new CapturingLogger<MapSession>();
        var session = new MapSession(_host, _prefs, _time, logs);
        SeedState(StatePath, StateWithToken() with
        {
            Combat = new CombatState(1, "e1", [new CombatEntry("e1", "t1", 10), new CombatEntry("e2", "fantasma", 5)]),
        });

        await session.OpenAsync(MapNote);

        (session.Notice, logs.Messages.Any(m => m.Contains("1 datos") && !m.Contains("Goblin"))).Should()
            .Be(("Se han corregido 1 datos de combate incoherentes del mapa.", true));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Combat_survives_closing_and_reopening_the_map()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.ReplaceState(s => TurnOps.Start(s, [new InitiativeEntry("t1", 12)], () => "e1").State));
        await _session.CloseAsync();

        await _session.OpenAsync(MapNote);

        _session.Editor!.State.Combat!.Entries.Should().ContainSingle().Which.TokenId.Should().Be("t1");
    }

    [Fact]
    public async Task History_survives_closing_and_reopening()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.ReplaceState(s => CombatHistory.Start(s, [new InitiativeEntry("t1", 12)], () => "e1").State));
        _session.Mutate(e => e.ReplaceState(s => CombatHistory.Record(s,
            s with { Tokens = [.. s.Tokens.Select(t => t with { HpCurrent = 4 })] }, "Goblin 1: −7 HP (4/11)", () => "l2")));
        await _session.CloseAsync();
        await _session.OpenAsync(MapNote);

        _session.Mutate(e => e.ReplaceState(CombatHistory.Undo));

        _session.Editor!.State.Tokens.Single().HpCurrent.Should().BeNull();
    }

    [Fact]
    public async Task External_reload_with_pending_local_combat_change_keeps_local_without_a_notice()
    {
        SeedState(StatePath, StateWithToken());
        await _session.OpenAsync(MapNote);
        _session.Mutate(e => e.ReplaceState(s => TurnOps.Start(s, [new InitiativeEntry("t1", 12)], () => "e1").State));
        var stats = Volatile.Read(ref _factory.StateStats);

        ReplaceExternally(StatePath, StateWithToken(x: 99));
        await WaitUntilAsync(() => Volatile.Read(ref _factory.StateStats) > stats);
        await _session.FlushAsync();

        (_session.Editor!.State.Combat is not null, _session.Notice).Should().Be((true, null));
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
            {
                Messages.Add(formatter(state, exception));
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
