using System.Text.Json;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class TvSessionMapTests : IAsyncLifetime
{
    private const string Cave = "Mapas/Boceto de la cueva.md";
    private const string Lighthouse = "Mapas/Plano del Faro.md";
    private static readonly TvFrame Square = new(0, 0, 900, 900);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FakeTvServer _server = new();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly VaultHost _host;
    private readonly MapSession _maps;
    private readonly MapLibrary _library;
    private readonly TvSession _tv;

    public TvSessionMapTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _maps = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        _library = new MapLibrary(_host, _maps);
        var images = new NoteImageResolver(_host);
        var feed = new TvMapFeed(_host, new TokenHitPoints(_host), images);
        _tv = new TvSession(_server, _host, _maps, feed, new TvContentFeed(_host, images, feed), _prefs, NullLogger<TvSession>.Instance);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _host.OpenAsync(_vault.Root);
        await _tv.EnableAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _maps.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    private JsonElement LastMessage => Root(_server.Published[^1]);

    private JsonElement LastMap => LastMessage.GetProperty("map");

    private static (double X, double Y, double Width, double Height) FrameOf(JsonElement map)
    {
        var frame = map.GetProperty("frame");
        return (Math.Round(frame.GetProperty("x").GetDouble(), 2), Math.Round(frame.GetProperty("y").GetDouble(), 2),
            Math.Round(frame.GetProperty("width").GetDouble(), 2), Math.Round(frame.GetProperty("height").GetDouble(), 2));
    }

    private static JsonElement? TokenIn(JsonElement map, string id) =>
        map.GetProperty("tokens").EnumerateArray().Cast<JsonElement?>().FirstOrDefault(t => t!.Value.GetProperty("id").GetString() == id);

    private async Task FixOnCaveAsync(TvFrame? visible = null)
    {
        await _maps.OpenAsync(Cave);
        await _tv.FixFrameAsync(Cave, visible ?? Square);
    }

    [Fact]
    public async Task Fix_frame_publishes_map_with_fitted_frame()
    {
        _server.ReportViewport(new TvViewport(1920, 1080));

        await FixOnCaveAsync(new TvFrame(0, 0, 1000, 1000));

        (_tv.Mode, _tv.MapPath, LastMessage.GetProperty("kind").GetString(), FrameOf(LastMap))
            .Should().Be((TvMode.Map, Cave, "map", (-388.89, 0d, 1777.78, 1000d)));
    }

    [Fact]
    public async Task Without_viewport_frame_uses_16_9()
    {
        await FixOnCaveAsync();

        FrameOf(LastMap).Should().Be((-350d, 0d, 1600d, 900d));
    }

    [Fact]
    public async Task New_viewport_refits_frame_and_republishes()
    {
        await FixOnCaveAsync();

        _server.ReportViewport(new TvViewport(1000, 1000));

        await WaitUntilAsync(() => FrameOf(LastMap) == (0d, 0d, 900d, 900d));
    }

    [Fact]
    public async Task Token_move_republishes_from_in_memory_state()
    {
        await FixOnCaveAsync();

        _maps.Mutate(e => e.MoveToken("t-bandido", 945, 665));

        await WaitUntilAsync(() => TokenIn(LastMap, "t-bandido")!.Value.GetProperty("x").GetDouble() == 945);
    }

    [Fact]
    public async Task Hidden_token_portrait_is_not_whitelisted()
    {
        await FixOnCaveAsync();
        _server.Files.Should().HaveCount(1);

        _maps.Mutate(e => e.SetHidden("t-lyra", true));

        await WaitUntilAsync(() => TokenIn(LastMap, "t-lyra") is null && _server.Files.Count == 0);
    }

    [Fact]
    public async Task Revealing_token_adds_its_portrait()
    {
        await _maps.OpenAsync(Cave);
        _maps.Mutate(e => e.SetHidden("t-lyra", true));
        await _tv.FixFrameAsync(Cave, Square);
        _server.Files.Should().BeEmpty();

        _maps.Mutate(e => e.SetHidden("t-lyra", false));

        await WaitUntilAsync(() => TokenIn(LastMap, "t-lyra")?.GetProperty("portraitUrl").GetString() is { } url
            && _server.Files.ContainsKey(url["files/".Length..]));
    }

    [Fact]
    public async Task Whitelist_contains_only_map_image_and_visible_portraits()
    {
        await _host.Current!.SaveAsync("Personajes/Sombra.md", "---\ntype: npc\nname: Sombra\nportrait: _assets/sombra.webp\n---\n",
            Core.Vault.Storage.WritePrecondition.Overwrite);
        await _maps.OpenAsync(Lighthouse);
        _maps.Mutate(e =>
        {
            e.AddToken(new TokenDraft("Lyra", "Personajes/Lyra.md", TokenSide.Pc, 1, null, null), 35, 35);
            var shadow = e.AddToken(new TokenDraft("Sombra", "Personajes/Sombra.md", TokenSide.Enemy, 1, null, null), 105, 35);
            return e.SetHidden(shadow.Id, true);
        });

        await _tv.FixFrameAsync(Lighthouse, Square);

        var tokens = LastMap.GetProperty("tokens");
        var referenced = new[] { LastMap.GetProperty("imageUrl").GetString()!, tokens[0].GetProperty("portraitUrl").GetString()! }
            .Select(url => url["files/".Length..]);
        (tokens.GetArrayLength(), _server.Files.Values.All(f => f.ContentType == "image/png")).Should().Be((1, true));
        _server.Files.Keys.Should().BeEquivalentTo(referenced);
    }

    [Fact]
    public async Task Opening_another_map_in_tablet_does_not_change_tv()
    {
        await FixOnCaveAsync();
        var published = _server.Published.Count;

        await _maps.OpenAsync(Lighthouse);
        _maps.Mutate(e => e.AddToken(new TokenDraft("Goblin", null, TokenSide.Enemy, 1, null, 7), 35, 35) is not null);
        await Task.Delay(200);

        (_server.Published.Count, _tv.MapPath, _tv.Mode).Should().Be((published, Cave, TvMode.Map));
    }

    [Fact]
    public async Task Reopening_tv_map_keeps_updating()
    {
        await FixOnCaveAsync();
        await _maps.OpenAsync(Lighthouse);
        await _maps.OpenAsync(Cave);

        _maps.Mutate(e => e.MoveToken("t-bandido", 945, 665));

        await WaitUntilAsync(() => TokenIn(LastMap, "t-bandido")!.Value.GetProperty("x").GetDouble() == 945);
    }

    [Fact]
    public async Task Deleted_tv_map_goes_back_to_waiting()
    {
        await FixOnCaveAsync();

        await _library.DeleteAsync(Cave);

        await WaitUntilAsync(() => _tv.Mode == TvMode.Waiting && LastMessage.GetProperty("kind").GetString() == "waiting");
        (_tv.MapPath, _server.Files.Count).Should().Be(((string?)null, 0));
    }

    [Fact]
    public async Task Renamed_tv_map_goes_back_to_waiting()
    {
        await FixOnCaveAsync();

        await _library.RenameAsync(Cave, "Cueva honda");

        await WaitUntilAsync(() => _tv.Mode == TvMode.Waiting && LastMessage.GetProperty("kind").GetString() == "waiting");
    }

    [Fact]
    public async Task Frame_is_remembered_per_map_while_enabled()
    {
        await FixOnCaveAsync(new TvFrame(0, 0, 1600, 900));
        await _maps.OpenAsync(Lighthouse);

        await _tv.FixFrameAsync(Lighthouse, new TvFrame(100, 100, 1600, 900));

        (_tv.FrameFor(Cave), _tv.FrameFor(Lighthouse), _tv.MapPath)
            .Should().Be((new TvFrame(0, 0, 1600, 900), new TvFrame(100, 100, 1600, 900), Lighthouse));
    }

    [Fact]
    public async Task Disable_forgets_frames()
    {
        await FixOnCaveAsync();

        await _tv.DisableAsync();
        await _tv.EnableAsync();

        (_tv.FrameFor(Cave), _tv.MapPath, _tv.Mode).Should().Be(((TvFrame?)null, (string?)null, TvMode.Waiting));
    }

    [Fact]
    public async Task Tablet_frame_is_shown_only_on_the_tv_map()
    {
        await FixOnCaveAsync(new TvFrame(0, 0, 1600, 900));

        (_tv.VisibleFrameFor(Cave), _tv.VisibleFrameFor(Lighthouse), _tv.VisibleFrameFor(null))
            .Should().Be((new TvFrame(0, 0, 1600, 900), (TvFrame?)null, (TvFrame?)null));
    }

    [Fact]
    public async Task Fixing_a_frame_on_a_map_that_is_not_open_does_nothing()
    {
        await _maps.OpenAsync(Lighthouse);

        await _tv.FixFrameAsync(Cave, Square);

        (_tv.Mode, _tv.MapPath).Should().Be((TvMode.Waiting, (string?)null));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(20);
        }
    }
}
