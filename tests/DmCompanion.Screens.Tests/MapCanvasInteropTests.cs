using System.Text.Json;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class MapCanvasInteropTests : IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly RecordingJs _js = new();
    private readonly GatedFactory _factory = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly MapCanvasInterop _interop;

    public MapCanvasInteropTests()
    {
        _host = new VaultHost(_factory, _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, new FakeTimeProvider(), NullLogger<MapSession>.Instance);
        var urls = new VaultFileUrls(_host);
        _interop = new MapCanvasInterop(_js, _session, urls, _host,
            new PortraitUrlProvider(_host, urls, NullLogger<PortraitUrlProvider>.Instance), NullLogger<MapCanvasInterop>.Instance);
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private async Task OpenAsync()
    {
        await _host.OpenAsync(_vault.Root);
        await _session.OpenAsync(MapTestVault.MapNote);
    }

    private static MapToken Token(MapEditor editor, string id) => editor.State.Tokens.Single(t => t.Id == id);

    private async Task<JsonElement> LoadedDtoAsync()
    {
        await _interop.LoadAsync(default);
        var call = _js.Calls.Single(c => c.Identifier == "mapCanvas.load");
        return JsonSerializer.SerializeToElement(call.Args[1], JsonSerializerOptions.Web);
    }

    [Fact]
    public async Task Load_sends_a_dto_with_a_vault_image_url_and_the_size_limit()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(true));
        await OpenAsync();

        var dto = await LoadedDtoAsync();

        dto.GetProperty("imageUrl").GetString().Should().StartWith("/_vault/Mapas/cripta.png?v=");
        dto.GetProperty("maxImageSide").GetInt32().Should().Be(8192);
        dto.GetProperty("readOnly").GetBoolean().Should().BeFalse();
        dto.GetProperty("grid").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Load_sends_tokens_with_faction_initial_and_portrait()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();

        var tokens = (await LoadedDtoAsync()).GetProperty("tokens").EnumerateArray().ToList();

        var goblin = tokens.Single(t => t.GetProperty("id").GetString() == "t1");
        goblin.GetProperty("faction").GetString().Should().Be("enemy");
        goblin.GetProperty("initial").GetString().Should().Be("G");
        goblin.GetProperty("portraitUrl").ValueKind.Should().Be(JsonValueKind.Null);
        var lyra = tokens.Single(t => t.GetProperty("id").GetString() == "t2");
        lyra.GetProperty("faction").GetString().Should().Be("pc");
        lyra.GetProperty("portraitUrl").GetString().Should().StartWith("/_vault/_assets/lyra.png?v=");
    }

    [Fact]
    public async Task Load_of_a_blank_canvas_has_no_image_url_and_the_canvas_size()
    {
        MapTestVault.WriteCanvasMap(_vault);
        await OpenAsync();

        var dto = await LoadedDtoAsync();

        dto.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        (dto.GetProperty("width").GetDouble(), dto.GetProperty("height").GetDouble()).Should().Be((1000, 800));
    }

    [Fact]
    public async Task Load_of_a_missing_image_still_sends_a_vault_url_so_the_canvas_reports_the_failure()
    {
        MapTestVault.WriteImageMap(_vault, withImage: false);
        await OpenAsync();

        var dto = await LoadedDtoAsync();

        dto.GetProperty("imageUrl").GetString().Should().StartWith("/_vault/Mapas/cripta.png");
    }

    [Fact]
    public async Task Render_does_nothing_before_the_canvas_is_loaded_and_sends_the_state_after()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();

        await _interop.RenderAsync();
        _js.Calls.Should().BeEmpty();

        await _interop.LoadAsync(default);
        _session.Mutate(e => e.MoveToken("t1", 50, 50));
        await _interop.RenderAsync();
        _js.Calls.Select(c => c.Identifier).Should().Equal("mapCanvas.load", "mapCanvas.render");
    }

    [Fact]
    public async Task A_mutation_while_the_render_resolves_portraits_still_repaints_afterwards()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        await _interop.LoadAsync(default);
        _session.Mutate(e => e.MoveToken("t1", 50, 50));
        var gate = _factory.Arm();

        var render = _interop.RenderAsync(); // se queda esperando al retrato
        _session.Mutate(e => e.MoveToken("t1", 60, 60));
        gate.SetResult();
        await render;
        await _interop.RenderAsync();

        _js.Calls.Count(c => c.Identifier == "mapCanvas.render").Should().Be(2);
        var last = JsonSerializer.SerializeToElement(_js.Calls.Last(c => c.Identifier == "mapCanvas.render").Args[0], JsonSerializerOptions.Web);
        last.GetProperty("tokens")[0].GetProperty("x").GetDouble().Should().Be(60);
    }

    [Fact]
    public async Task A_mutation_while_the_load_resolves_portraits_still_repaints_afterwards()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        var gate = _factory.Arm();

        var load = _interop.LoadAsync(default);
        _session.Mutate(e => e.MoveToken("t1", 60, 60));
        gate.SetResult();
        await load;
        await _interop.RenderAsync();

        _js.Calls.Select(c => c.Identifier).Should().Equal("mapCanvas.load", "mapCanvas.render");
    }

    [Fact]
    public async Task Fit_and_SetMode_call_the_module()
    {
        MapTestVault.WriteImageMap(_vault);
        await OpenAsync();
        await _interop.LoadAsync(default);

        await _interop.FitAsync();
        await _interop.SetModeAsync(MapMode.Draw, new DrawTool("#112233", 4, false));

        _js.Calls.Select(c => c.Identifier).Should().Equal("mapCanvas.load", "mapCanvas.fit", "mapCanvas.setMode");
        JsonSerializer.SerializeToElement(_js.Calls[2].Args[1], JsonSerializerOptions.Web).GetProperty("color").GetString().Should().Be("#112233");
    }

    private async Task<MapStroke> AddedStrokeAsync()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        _interop.OnStrokeAdded("#9E2B25", 6, [10, 20, 0.5, 30, 40, 0.8]);
        return _session.Editor!.State.Strokes.Should().ContainSingle().Subject;
    }

    [Fact]
    public async Task StrokeAdded_stores_color_and_width()
    {
        var stroke = await AddedStrokeAsync();

        (stroke.Color, stroke.Width).Should().Be(("#9E2B25", 6.0));
    }

    [Fact]
    public async Task StrokeAdded_stores_the_points_with_their_pressure()
    {
        var stroke = await AddedStrokeAsync();

        stroke.Points.Should().Equal(10, 20, 0.5, 30, 40, 0.8);
    }

    [Fact]
    public async Task StrokeAdded_can_be_undone()
    {
        await AddedStrokeAsync();

        _session.Editor!.CanUndo.Should().BeTrue();
    }

    [Theory]
    [InlineData("", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("red", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000000\n", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#12345g", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#0000000", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("url(javascript:x)", 6, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000000", 0.5, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000000", 101, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000000", 0, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000000", double.NaN, new[] { 1.0, 2, 0.5, 3, 4, 0.5 })]
    [InlineData("#000000", 6, new[] { 1.0, 2, 0.5, 3, 4 })]
    [InlineData("#000000", 6, new[] { 1.0, 2, 0.5 })]
    [InlineData("#000000", 6, new[] { 1.0, 2, 0.5, double.PositiveInfinity, 4, 0.5 })]
    public async Task StrokeAdded_ignores_malformed_input(string color, double width, double[] points)
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        var before = _session.Editor!.State;

        _interop.OnStrokeAdded(color, width, points);

        _session.Editor!.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task StrokeAdded_reports_that_it_accepted_the_stroke()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();

        _interop.OnStrokeAdded("#9E2B25", 6, [1, 2, 0.5, 3, 4, 0.5]).Should().BeTrue();
    }

    [Fact]
    public async Task StrokeAdded_reports_that_it_rejected_a_read_only_stroke()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "esto no es json");
        await OpenAsync();

        _interop.OnStrokeAdded("#000000", 6, [1, 2, 0.5, 3, 4, 0.5]).Should().BeFalse();
    }

    [Fact]
    public async Task StrokeAdded_in_read_only_mode_changes_nothing()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "esto no es json");
        await OpenAsync();
        var before = _session.Editor!.State;

        _interop.OnStrokeAdded("#000000", 6, [1, 2, 0.5, 3, 4, 0.5]);

        _session.Editor!.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task StrokeErased_removes_the_stroke()
    {
        var id = (await AddedStrokeAsync()).Id;

        _interop.OnStrokeErased(id);

        _session.Editor!.State.Strokes.Should().BeEmpty();
    }

    [Fact]
    public async Task StrokeErased_can_be_undone()
    {
        var id = (await AddedStrokeAsync()).Id;
        _interop.OnStrokeErased(id);

        _session.Editor!.Undo();

        _session.Editor.State.Strokes.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [Fact]
    public async Task StrokeErased_of_an_unknown_id_changes_nothing()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        _interop.OnStrokeAdded("#000000", 6, [1, 2, 0.5, 3, 4, 0.5]);
        var before = _session.Editor!.State;

        _interop.OnStrokeErased("nope");

        _session.Editor.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task The_chosen_mode_is_remembered_and_sent_again_when_the_canvas_loads()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        await _interop.SetModeAsync(MapMode.Draw, new DrawTool("#112233", 4, true)); // aún sin lienzo: no llama

        await _interop.LoadAsync(default);

        _js.Calls.Select(c => c.Identifier).Should().Equal("mapCanvas.load", "mapCanvas.setMode");
        (_interop.Mode, _interop.Tool).Should().Be((MapMode.Draw, new DrawTool("#112233", 4, true)));
    }

    [Fact]
    public async Task Load_sends_the_unit_of_the_active_game_system_for_measuring()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(true));
        await OpenAsync();

        var dto = await LoadedDtoAsync();

        (dto.GetProperty("unitAmount").GetDouble(), dto.GetProperty("unitLabel").GetString()).Should().Be((5, "pies"));
    }

    [Fact]
    public async Task Moving_a_token_snaps_the_center_when_the_grid_is_on()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(true));
        await OpenAsync();

        _interop.OnTokenMoved("t1", 100, 100);

        var moved = Token(_session.Editor!, "t1");
        (moved.X, moved.Y).Should().Be((105, 105));
    }

    [Fact]
    public async Task Moving_a_token_keeps_the_coordinates_when_the_grid_is_off()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();

        _interop.OnTokenMoved("t1", 100, 100);

        var moved = Token(_session.Editor!, "t1");
        (moved.X, moved.Y).Should().Be((100, 100));
    }

    [Fact]
    public async Task Moving_a_token_in_read_only_mode_changes_nothing()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "esto no es json");
        await OpenAsync();
        _session.IsReadOnly.Should().BeTrue();
        var before = _session.Editor!.State;

        _interop.OnTokenMoved("t1", 100, 100);

        _session.Editor!.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task Moving_an_unknown_token_changes_nothing()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(true));
        await OpenAsync();
        var before = _session.Editor!.State;

        _interop.OnTokenMoved("nope", 100, 100);

        _session.Editor!.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task ImageLoaded_stores_the_real_image_size()
    {
        MapTestVault.WriteImageMap(_vault);
        await OpenAsync();

        _interop.OnImageLoaded(MapTestVault.MapNote, 4000, 3000);

        (_session.Current!.Width, _session.Current.Height).Should().Be((4000, 3000));
    }

    [Fact]
    public async Task A_late_image_event_of_another_map_is_ignored()
    {
        MapTestVault.WriteImageMap(_vault);
        await OpenAsync();

        _interop.OnImageLoaded("Mapas/otro.md", 4000, 3000);
        _interop.OnImageFailed("Mapas/otro.md");

        (_session.Current!.Width, _session.ImageNotice).Should().Be((0, null));
    }

    [Fact]
    public async Task A_session_change_without_a_new_state_does_not_repaint()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        await _interop.LoadAsync(default);

        await _interop.RenderAsync(); // mismo State que el de la carga: nada que pintar
        _session.Mutate(e => e.MoveToken("t1", 50, 50));
        await _interop.RenderAsync();
        await _interop.RenderAsync();

        _js.Calls.Select(c => c.Identifier).Should().Equal("mapCanvas.load", "mapCanvas.render");
    }

    [Fact]
    public async Task ImageFailed_sets_the_notice()
    {
        MapTestVault.WriteImageMap(_vault, withImage: false);
        await OpenAsync();

        _interop.OnImageFailed(MapTestVault.MapNote);

        _session.ImageNotice.Should().Be("No se encuentra la imagen del mapa");
    }

    [Fact]
    public async Task Load_with_an_unsafe_image_path_sets_the_missing_image_notice()
    {
        MapTestVault.WriteImageMap(_vault);
        File.WriteAllText(_vault.PathOf(MapTestVault.MapNote), "---\ntype: map\nname: Cripta\nimage: ../fuera.png\n---\n");
        await OpenAsync();

        await _interop.LoadAsync(default);

        _session.ImageNotice.Should().Be("No se encuentra la imagen del mapa");
    }

    [Fact]
    public async Task TokenTapped_is_raised_with_the_screen_position()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenAsync();
        TokenTappedEventArgs? tapped = null;
        _interop.TokenTapped += (_, e) => tapped = e;

        _interop.OnTokenTapped("t1", 12, 34);

        tapped.Should().Be(new TokenTappedEventArgs("t1", 12, 34));
    }

    [Fact]
    public async Task A_closed_circuit_does_not_break_the_calls()
    {
        MapTestVault.WriteImageMap(_vault);
        await OpenAsync();
        _js.Failure = new JSDisconnectedException("gone");

        var act = async () =>
        {
            await _interop.LoadAsync(default);
            await _interop.RenderAsync();
            await _interop.FitAsync();
        };

        await act.Should().NotThrowAsync();
    }

    // Detiene el StatAsync del retrato hasta que el test lo libere, para provocar un Mutate a mitad de BuildDtoAsync.
    private sealed class GatedFactory : IVaultServiceFactory
    {
        public volatile TaskCompletionSource? Gate;

        public TaskCompletionSource Arm() => Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public VaultService Create(string rootPath) => new(
            new GatedStorage(new FileSystemVaultStorage(rootPath), this),
            new FileSystemVaultWatcher(rootPath),
            NullLogger<VaultService>.Instance);
    }

    private sealed class GatedStorage(IVaultStorage inner, GatedFactory owner) : IVaultStorage
    {
        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public Task<VaultFileContent> ReadAsync(string p, CancellationToken ct = default) => inner.ReadAsync(p, ct);
        public Task<byte[]> ReadBytesAsync(string p, CancellationToken ct = default) => inner.ReadBytesAsync(p, ct);
        public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default) => inner.ListFilesAsync(f, ct);
        public Task<Stream> OpenReadAsync(string p, CancellationToken ct = default) => inner.OpenReadAsync(p, ct);
        public async Task<VaultFileInfo?> StatAsync(string p, CancellationToken ct = default)
        {
            if (p == MapTestVault.Portrait && owner.Gate is { } gate) await gate.Task;
            return await inner.StatAsync(p, ct);
        }
        public Task<WriteResult> WriteAsync(string p, string c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteAsync(p, c, pre, ct);
        public Task<WriteResult> WriteBytesAsync(string p, Stream c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteBytesAsync(p, c, pre, ct);
        public Task<bool> MoveAsync(string a, string b, CancellationToken ct = default) => inner.MoveAsync(a, b, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string p, CancellationToken ct = default) => inner.DeleteAsync(p, ct);
    }

    private sealed class RecordingJs : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];
        public Exception? Failure { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (Failure is not null) throw Failure;
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
