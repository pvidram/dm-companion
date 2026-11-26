using System.Text.Json;
using DmCompanion.Core.Maps;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class MapCanvasFogTests : IAsyncLifetime
{
    private static readonly double[] Triangle = [0, 0, 100, 0, 0, 100];

    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly RecordingJs _js = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly MapCanvasInterop _interop;

    public MapCanvasFogTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        var urls = new VaultFileUrls(_host);
        _interop = new MapCanvasInterop(_js, _session, urls, _host,
            new PortraitUrlProvider(_host, urls, NullLogger<PortraitUrlProvider>.Instance), new ShellState(), NullLogger<MapCanvasInterop>.Instance);
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private async Task OpenAsync(string? rawState = null)
    {
        MapTestVault.WriteImageMap(_vault, rawState is null ? MapTestVault.StateWithGrid(false) : null, rawState);
        await _host.OpenAsync(_vault.Root);
        await _session.OpenAsync(MapTestVault.MapNote);
    }

    private IReadOnlyList<MapFogZone> Zones => _session.Editor!.State.FogZones;

    [Fact]
    public async Task Adding_a_valid_zone_adds_it_covered()
    {
        await OpenAsync();

        _interop.OnFogZoneAdded(Triangle).Should().BeTrue();

        Zones.Should().ContainSingle().Which.IsRevealed.Should().BeFalse();
    }

    [Fact]
    public async Task Adding_a_zone_with_two_corners_is_rejected_and_changes_nothing()
    {
        await OpenAsync();
        var before = _session.Editor!.State;

        _interop.OnFogZoneAdded([0, 0, 100, 0]).Should().BeFalse();

        _session.Editor.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task Toggling_a_zone_reveals_it_and_toggling_again_covers_it()
    {
        await OpenAsync();
        _interop.OnFogZoneAdded(Triangle);
        var id = Zones.Single().Id;

        _interop.OnFogZoneToggled(id);
        var revealed = Zones.Single().IsRevealed;
        _interop.OnFogZoneToggled(id);

        (revealed, Zones.Single().IsRevealed).Should().Be((true, false));
    }

    [Fact]
    public async Task Erasing_a_zone_removes_it()
    {
        await OpenAsync();
        _interop.OnFogZoneAdded(Triangle);

        _interop.OnFogZoneErased(Zones.Single().Id);

        Zones.Should().BeEmpty();
    }

    [Fact]
    public void The_default_zone_type_is_total() => _interop.FogType.Should().Be(FogType.Total);

    [Fact]
    public async Task Adding_a_zone_uses_the_chosen_type()
    {
        await OpenAsync();
        _interop.SetFogType(FogType.Npc);

        _interop.OnFogZoneAdded(Triangle);

        Zones.Single().EffectiveType.Should().Be(FogType.Npc);
    }

    [Fact]
    public async Task Retyping_a_zone_applies_the_chosen_type_and_is_undoable()
    {
        await OpenAsync();
        _interop.OnFogZoneAdded(Triangle);
        _interop.SetFogType(FogType.Npc);

        _interop.OnFogZoneRetyped(Zones.Single().Id);
        var changed = Zones.Single().EffectiveType;
        _session.Mutate(e => e.Undo());

        (changed, Zones.Single().EffectiveType).Should().Be((FogType.Npc, FogType.Total));
    }

    [Fact]
    public async Task Retyping_to_the_type_it_already_has_changes_nothing()
    {
        await OpenAsync();
        _interop.OnFogZoneAdded(Triangle);
        var before = _session.Editor!.State;

        _interop.OnFogZoneRetyped(Zones.Single().Id);

        _session.Editor.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task Retyping_in_a_read_only_session_changes_nothing()
    {
        await OpenAsync(rawState: "{\"version\":5,\"tokens\":[],\"strokes\":[],\"fog\":[{\"id\":\"z1\",\"points\":[0,0,100,0,0,100]}]}");
        _session.IsReadOnly.Should().BeTrue();
        Zones.Single().IsNpc.Should().BeFalse();
        _interop.SetFogType(FogType.Npc);

        _interop.OnFogZoneRetyped("z1");

        Zones.Single().IsNpc.Should().BeFalse();
    }

    [Fact]
    public async Task Choosing_the_freehand_tool_sends_it_to_the_canvas()
    {
        await OpenAsync();
        await _interop.LoadAsync(default);

        await _interop.SetFogToolAsync(FogTool.Freehand);

        _js.Calls.Should().ContainSingle(c => c.Identifier == "mapCanvas.setFogTool").Which.Args.Should().Equal("Freehand");
    }

    [Fact]
    public async Task The_canvas_dto_carries_the_zone_type()
    {
        await OpenAsync();
        _interop.SetFogType(FogType.Npc);
        _interop.OnFogZoneAdded(Triangle);
        _interop.SetFogType(FogType.Total);
        _interop.OnFogZoneAdded(Triangle);

        await _interop.LoadAsync(default);

        var dto = JsonSerializer.SerializeToElement(_js.Calls.Single(c => c.Identifier == "mapCanvas.load").Args[1], JsonSerializerOptions.Web);
        dto.GetProperty("fog").EnumerateArray().Select(z => z.GetProperty("type").GetString()).Should().Equal("npc", "total");
    }

    [Fact]
    public async Task Read_only_session_changes_nothing()
    {
        await OpenAsync(rawState: "esto no es json");
        _session.IsReadOnly.Should().BeTrue();
        var before = _session.Editor!.State;

        _interop.OnFogZoneAdded(Triangle).Should().BeFalse();
        _interop.OnFogZoneToggled("z1");
        _interop.OnFogZoneErased("z1");

        _session.Editor.State.Should().BeSameAs(before);
    }

    [Fact]
    public async Task Choosing_a_tool_sends_it_to_the_canvas_and_remembers_it()
    {
        await OpenAsync();
        await _interop.LoadAsync(default);

        await _interop.SetFogToolAsync(FogTool.Trace);

        _js.Calls.Should().ContainSingle(c => c.Identifier == "mapCanvas.setFogTool")
            .Which.Args.Should().Equal("Trace");
        _interop.FogTool.Should().Be(FogTool.Trace);
    }

    [Fact]
    public void The_default_tool_is_reveal()
    {
        _interop.FogTool.Should().Be(FogTool.Reveal);
    }

    [Fact]
    public void Draft_changes_update_the_corner_count_and_notify()
    {
        var raised = 0;
        _interop.FogDraftChanged += (_, _) => raised++;

        _interop.OnFogDraftChanged(2);

        (_interop.FogDraftCorners, raised).Should().Be((2, 1));
    }

    [Fact]
    public async Task Leaving_fog_mode_clears_the_draft_count()
    {
        _interop.OnFogDraftChanged(2);

        await _interop.SetModeAsync(MapMode.Navigate, DrawTool.Default);

        _interop.FogDraftCorners.Should().Be(0);
    }

    [Fact]
    public async Task Undo_and_cancel_are_sent_to_the_canvas()
    {
        await OpenAsync();
        await _interop.LoadAsync(default);

        await _interop.UndoFogCornerAsync();
        await _interop.CancelFogDraftAsync();

        _js.Calls.Where(c => c.Identifier == "mapCanvas.fogDraft").Select(c => c.Args[0]).Should().Equal("undo", "cancel");
    }

    [Fact]
    public async Task The_canvas_dto_carries_the_fog_zones()
    {
        await OpenAsync();
        _interop.OnFogZoneAdded(Triangle);
        _interop.OnFogZoneToggled(Zones.Single().Id);

        await _interop.LoadAsync(default);

        var dto = JsonSerializer.SerializeToElement(_js.Calls.Single(c => c.Identifier == "mapCanvas.load").Args[1], JsonSerializerOptions.Web);
        var zone = dto.GetProperty("fog").EnumerateArray().Single();
        (zone.GetProperty("id").GetString(), zone.GetProperty("revealed").GetBoolean(), zone.GetProperty("points").GetArrayLength())
            .Should().Be((Zones.Single().Id, true, 6));
    }

    private sealed class RecordingJs : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
