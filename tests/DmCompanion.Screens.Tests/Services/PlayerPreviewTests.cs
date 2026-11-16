using DmCompanion.Screens.Tests.Support;
using System.Text.Json;
using DmCompanion.Core.Maps;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public sealed class PlayerPreviewTests : IAsyncLifetime
{
    private const string GhostNote = "Personajes/fantasma.md";

    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly NoteImageResolver _images;
    private readonly PlayerPreview _preview;

    public PlayerPreviewTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        _images = new NoteImageResolver(_host);
        _preview = new PlayerPreview(_session, _host, new TokenHitPoints(_host, GlossaryTestServices.CreateGlossary(_host)), _images);
    }

    async Task IAsyncLifetime.InitializeAsync() => await _host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        _preview.Dispose();
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private static readonly MapFogZone CoverGoblin = new("z1", [0, 0, 70, 0, 70, 70, 0, 70]);

    private async Task OpenMapAsync(MapState? state = null)
    {
        var withGhost = (state ?? MapTestVault.StateWithGrid(false)) with
        {
            Tokens = [.. (state ?? MapTestVault.StateWithGrid(false)).Tokens,
                new MapToken("t3", "Fantasma", GhostNote, TokenSide.Pc, 1, 175, 35, null, null)],
        };
        MapTestVault.WriteImageMap(_vault, withGhost);
        File.WriteAllText(_vault.PathOf(GhostNote), "---\ntype: pc\nname: Fantasma\nportrait: _assets/no_existe.png\n---\n");
        await _host.OpenAsync(_vault.Root);
        await _session.OpenAsync(MapTestVault.MapNote);
        await _images.EnsureLoadedAsync();
    }

    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public void Toggle_flips_the_state_and_raises_Changed()
    {
        var raised = 0;
        _preview.Changed += (_, _) => raised++;

        _preview.Toggle();
        var afterFirst = _preview.IsActive;
        _preview.Toggle();

        (afterFirst, _preview.IsActive, raised).Should().Be((true, false, 2));
    }

    [Fact]
    public async Task Without_an_open_map_BuildJson_is_null()
    {
        await _host.OpenAsync(_vault.Root);

        _preview.BuildJson().Should().BeNull();
    }

    [Fact]
    public async Task Fog_is_sent_and_the_frame_is_the_whole_map()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(false) with { Fog = [CoverGoblin] });

        var map = Parse(_preview.BuildJson());

        (map.GetProperty("fog").GetArrayLength(), map.GetProperty("frame").ValueKind).Should().Be((1, JsonValueKind.Null));
    }

    [Fact]
    public async Task A_token_under_covered_fog_is_not_sent()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(false) with { Fog = [CoverGoblin] });

        _preview.BuildJson().Should().NotContain("Goblin");
    }

    [Fact]
    public async Task Image_urls_use_the_tablet_vault_prefix()
    {
        await OpenMapAsync();

        var map = Parse(_preview.BuildJson());
        var lyra = map.GetProperty("tokens").EnumerateArray().Single(t => t.GetProperty("id").GetString() == "t2");

        (map.GetProperty("imageUrl").GetString(), lyra.GetProperty("portraitUrl").GetString())
            .Should().Match<(string?, string?)>(urls => urls.Item1!.StartsWith(VaultFileRequest.Prefix) && urls.Item2!.StartsWith(VaultFileRequest.Prefix));
    }

    [Fact]
    public async Task A_missing_portrait_is_sent_as_null_so_the_initial_is_drawn()
    {
        await OpenMapAsync();

        var ghost = Parse(_preview.BuildJson()).GetProperty("tokens").EnumerateArray()
            .Single(t => t.GetProperty("id").GetString() == "t3");

        ghost.GetProperty("portraitUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Closing_the_map_deactivates_the_view()
    {
        await OpenMapAsync();
        _preview.Toggle();

        await _session.CloseAsync();

        _preview.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Opening_another_map_keeps_the_view_active()
    {
        await OpenMapAsync();
        _preview.Toggle();

        await _session.OpenAsync(MapTestVault.MapNote);

        _preview.IsActive.Should().BeTrue();
    }
}
