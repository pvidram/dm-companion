using DmCompanion.Screens.Tests.Support;
using System.Text.Json;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class TvMapFeedTests : IAsyncLifetime
{
    private const string Cave = "Mapas/Boceto de la cueva.md";
    private const string Lighthouse = "Mapas/Plano del Faro.md";
    private static readonly TvFrame Frame = new(0, 0, 1600, 900);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly VaultHost _host;
    private readonly NoteImageResolver _images;
    private readonly TvMapFeed _feed;

    public TvMapFeedTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        _images = new NoteImageResolver(_host);
        _feed = new TvMapFeed(_host, new TokenHitPoints(_host, GlossaryTestServices.CreateGlossary(_host)), _images);
    }

    Task IAsyncLifetime.InitializeAsync() => _host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private MapDefinition Map(string path) => MapDefinition.FromEntity(_host.Current!.Index.Get(path)!)!;

    private MapState CaveState() =>
        MapStateSerializer.Read(File.ReadAllText(_vault.PathOf("Mapas/Boceto de la cueva.state.json"))).State;

    private static JsonElement MapOf(TvMapMessage message) => JsonDocument.Parse(message.Json).RootElement.GetProperty("map");

    [Fact]
    public void Message_follows_the_tv_format()
    {
        var message = _feed.Build(Map(Cave), CaveState(), Frame);

        var root = JsonDocument.Parse(message.Json).RootElement;
        var map = root.GetProperty("map");
        var lyra = map.GetProperty("tokens")[0];
        (root.GetProperty("kind").GetString(), map.GetProperty("imageUrl").ValueKind, map.GetProperty("grid").GetProperty("cellSize").GetDouble(),
         map.GetProperty("frame").GetProperty("width").GetDouble(), lyra.GetProperty("faction").GetString(), lyra.GetProperty("initial").GetString(),
         map.GetProperty("strokes")[0].GetProperty("id").GetString(), map.GetProperty("combat").ValueKind)
            .Should().Be(("map", JsonValueKind.Null, 70d, 1600d, "pc", "L", "s-muro", JsonValueKind.Null));
    }

    [Fact]
    public void Message_carries_fog_and_omits_tokens_under_it()
    {
        var lyra = CaveState().Tokens[0];
        var state = CaveState() with { Fog = [new MapFogZone("z", [lyra.X - 50, lyra.Y - 50, lyra.X + 50, lyra.Y - 50, lyra.X + 50, lyra.Y + 50])] };

        var map = MapOf(_feed.Build(Map(Cave), state, Frame));

        (map.GetProperty("fog").GetArrayLength(), map.GetProperty("fog")[0].GetArrayLength(),
         string.Join(",", map.GetProperty("tokens").EnumerateArray().Select(t => t.GetProperty("id").GetString())))
            .Should().Be((1, 6, "t-bandido"));
    }

    [Fact]
    public void Message_carries_no_vault_path()
    {
        var message = _feed.Build(Map(Cave), CaveState(), Frame);

        (message.Json.Contains("Mapas/"), message.Json.Contains("_assets"), message.Json.Contains("mapPath", StringComparison.OrdinalIgnoreCase))
            .Should().Be((false, false, false));
    }

    [Fact]
    public void Image_map_sends_zero_size_and_a_whitelisted_image_url()
    {
        var map = Map(Lighthouse) with { Width = 1200, Height = 900 };

        var message = _feed.Build(map, MapState.Empty, Frame);

        var json = MapOf(message);
        var id = json.GetProperty("imageUrl").GetString()!["files/".Length..];
        (json.GetProperty("width").GetDouble(), json.GetProperty("height").GetDouble(), message.Files[id].ContentType)
            .Should().Be((0d, 0d, "image/png"));
    }

    [Fact]
    public void Portrait_url_points_to_an_opaque_whitelisted_id()
    {
        var message = _feed.Build(Map(Cave), CaveState(), Frame);

        var url = MapOf(message).GetProperty("tokens")[0].GetProperty("portraitUrl").GetString()!;
        var id = url.StartsWith("files/") ? url["files/".Length..] : url;
        (id.All(char.IsDigit), message.Files.Keys.Single()).Should().Be((true, id));
    }

    [Fact]
    public void Ids_are_stable_between_messages()
    {
        var first = _feed.Build(Map(Cave), CaveState(), Frame);

        var second = _feed.Build(Map(Cave), CaveState(), Frame with { X = 10 });

        second.Files.Keys.Should().Equal(first.Files.Keys);
    }

    [Fact]
    public async Task Whitelisted_file_opens_from_the_current_vault()
    {
        var message = _feed.Build(Map(Lighthouse), MapState.Empty, Frame);

        await using var stream = await message.Files.Values.Single().Open(CancellationToken.None);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        copy.ToArray().Should().Equal(File.ReadAllBytes(_vault.PathOf("Mapas/Plano del Faro.png")));
    }

    [Theory]
    [InlineData("a/b.png", "image/png")]
    [InlineData("a/b.JPG", "image/jpeg")]
    [InlineData("a/b.jpeg", "image/jpeg")]
    [InlineData("a/b.webp", "image/webp")]
    [InlineData("a/b.gif", "image/gif")]
    [InlineData("a/b.svg", null)]
    [InlineData("a/b.md", null)]
    public void Content_type_comes_from_the_extension(string path, string? expected) =>
        TvMapFeed.ContentTypeFor(path).Should().Be(expected);

    [Fact]
    public async Task Portrait_with_an_unsupported_extension_is_not_sent()
    {
        var state = CaveState() with { Tokens = [CaveState().Tokens[0] with { Source = "Personajes/Torvald.md" }] };
        await _host.Current!.SaveAsync("Personajes/Torvald.md", "---\ntype: pc\nname: Torvald\nportrait: _assets/torvald.svg\n---\n",
            Core.Vault.Storage.WritePrecondition.Overwrite);

        var message = _feed.Build(Map(Cave), state, Frame);

        (MapOf(message).GetProperty("tokens")[0].GetProperty("portraitUrl").ValueKind, message.Files.Count).Should().Be((JsonValueKind.Null, 0));
    }

    [Theory]
    [InlineData("pc", true)]
    [InlineData("npc", false)]
    public async Task Aliased_token_keeps_the_portrait_only_when_its_note_is_a_pc(string type, bool keeps)
    {
        var state = CaveState() with { Tokens = [CaveState().Tokens[0] with { Source = "Personajes/Sombra.md", Alias = "Sombra" }] };
        await _host.Current!.SaveAsync("Personajes/Sombra.md", $"---\ntype: {type}\nname: Torvald\nportrait: _assets/x.png\n---\n",
            Core.Vault.Storage.WritePrecondition.Overwrite);

        var token = MapOf(_feed.Build(Map(Cave), state, Frame)).GetProperty("tokens")[0];

        (token.GetProperty("portraitUrl").ValueKind != JsonValueKind.Null, token.GetProperty("name").GetString()).Should().Be((keeps, "Sombra"));
    }

    [Fact]
    public async Task Whitelisted_file_is_not_opened_from_another_vault_after_a_switch()
    {
        var message = _feed.Build(Map(Lighthouse), MapState.Empty, Frame);
        using var other = TempVault.FromDemo();
        File.WriteAllBytes(other.PathOf("Mapas/Plano del Faro.png"), [9, 9, 9]);
        await _host.OpenAsync(other.Root);

        var open = () => message.Files.Values.Single().Open(CancellationToken.None);

        await open.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Replaced_image_at_the_same_path_gets_a_new_url()
    {
        var map = Map(Lighthouse);
        await _images.EnsureLoadedAsync();
        var before = MapOf(_feed.Build(map, MapState.Empty, Frame)).GetProperty("imageUrl").GetString();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _images.ImagesChanged += (_, _) => changed.TrySetResult();
        var png = _vault.PathOf("Mapas/Plano del Faro.png");
        File.WriteAllBytes(png, [.. File.ReadAllBytes(png), 0]);
        File.SetLastWriteTimeUtc(png, DateTime.UtcNow.AddMinutes(5));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await _images.EnsureLoadedAsync();

        var after = MapOf(_feed.Build(map, MapState.Empty, Frame)).GetProperty("imageUrl").GetString();

        after.Should().NotBe(before);
    }

    [Fact]
    public async Task Same_path_in_another_vault_gets_a_new_url()
    {
        var before = MapOf(_feed.Build(Map(Lighthouse), MapState.Empty, Frame)).GetProperty("imageUrl").GetString();
        using var other = TempVault.FromDemo();
        await _host.OpenAsync(other.Root);

        var after = MapOf(_feed.Build(Map(Lighthouse), MapState.Empty, Frame)).GetProperty("imageUrl").GetString();

        after.Should().NotBe(before);
    }

    [Fact]
    public void Ids_are_not_reused_after_a_reset()
    {
        var before = MapOf(_feed.Build(Map(Lighthouse), MapState.Empty, Frame)).GetProperty("imageUrl").GetString();
        _feed.Reset();

        var after = MapOf(_feed.Build(Map(Lighthouse), MapState.Empty, Frame)).GetProperty("imageUrl").GetString();

        after.Should().NotBe(before);
    }
}
