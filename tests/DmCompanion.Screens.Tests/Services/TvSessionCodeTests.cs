using System.Text.Json;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class TvSessionCodeTests : IAsyncLifetime
{
    private const string Key = "tv.code";

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly FakeTvServer _server = new();
    private readonly VaultHost _host;
    private readonly MapSession _maps;
    private readonly TvSession _tv;

    public TvSessionCodeTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        _maps = new MapSession(_host, new InMemoryAppPreferences(), TimeProvider.System, NullLogger<MapSession>.Instance);
        _tv = NewSession(_server);
    }

    private TvSession NewSession(FakeTvServer server)
    {
        var images = new NoteImageResolver(_host);
        var feed = new TvMapFeed(_host, new TokenHitPoints(_host, GlossaryTestServices.CreateGlossary(_host)), images);
        return new TvSession(server, _host, _maps, feed, new TvContentFeed(_host, images, feed), _prefs, NullLogger<TvSession>.Instance);
    }

    Task IAsyncLifetime.InitializeAsync() => _host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _maps.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    [Fact]
    public async Task First_enable_saves_the_suggested_code()
    {
        await _tv.EnableAsync();

        (_tv.Code, _prefs.Get(Key), _server.StartedCode).Should().Be(("costa-de-sal", "costa-de-sal", "costa-de-sal"));
    }

    [Fact]
    public async Task Enable_reuses_the_saved_code()
    {
        await _tv.EnableAsync();
        await _tv.DisableAsync();
        await _tv.SetCodeAsync("brumavela");

        await _tv.EnableAsync();

        _server.StartedCode.Should().Be("brumavela");
    }

    [Fact]
    public void New_session_reuses_the_saved_code()
    {
        _prefs.Set(Key, "Brumavela");

        var other = NewSession(new FakeTvServer());

        other.Code.Should().Be("brumavela");
    }

    [Fact]
    public async Task Invalid_saved_code_is_replaced_by_suggestion()
    {
        _prefs.Set(Key, "a_b");

        await _tv.EnableAsync();

        (_tv.Code, _prefs.Get(Key)).Should().Be(("costa-de-sal", "costa-de-sal"));
    }

    [Fact]
    public void Code_is_known_while_off() => _tv.Code.Should().Be("costa-de-sal");

    [Theory]
    [InlineData("abc")]
    [InlineData("-mesa")]
    [InlineData("mesa_1")]
    public async Task SetCode_rejects_invalid_code(string code)
    {
        await _tv.EnableAsync();

        var accepted = await _tv.SetCodeAsync(code);

        (accepted, _tv.Code, _server.StartedCode).Should().Be((false, "costa-de-sal", "costa-de-sal"));
    }

    [Fact]
    public async Task SetCode_while_off_saves_it()
    {
        var accepted = await _tv.SetCodeAsync("  BrumaVela ");

        (accepted, _tv.Code, _prefs.Get(Key), _server.IsRunning).Should().Be((true, "brumavela", "brumavela", false));
    }

    [Fact]
    public async Task SetCode_while_enabled_restarts_with_new_code_and_keeps_mode()
    {
        await _maps.OpenAsync("Mapas/Boceto de la cueva.md");
        await _tv.EnableAsync();
        await _tv.FixFrameAsync("Mapas/Boceto de la cueva.md", new TvFrame(0, 0, 900, 900));
        var map = _server.Published[^1];

        var accepted = await _tv.SetCodeAsync("brumavela");

        (accepted, _server.StartedCode, _server.IsRunning, _tv.Mode, _server.Published[^1], _tv.AddressFor("1.2.3.4"))
            .Should().Be((true, "brumavela", true, TvMode.Map, map, $"http://1.2.3.4:{FakeTvServer.Port}/tv/brumavela/"));
    }

    [Fact]
    public async Task SetCode_while_waiting_republishes_waiting()
    {
        await _tv.EnableAsync();
        var before = _server.Published.Count;

        await _tv.SetCodeAsync("brumavela");

        (_server.Published.Count, JsonDocument.Parse(_server.Published[^1]).RootElement.GetProperty("kind").GetString())
            .Should().Be((before + 1, "waiting"));
    }

    [Fact]
    public async Task SetCode_with_the_same_code_does_not_restart()
    {
        await _tv.EnableAsync();
        var before = _server.Published.Count;

        var accepted = await _tv.SetCodeAsync("COSTA-DE-SAL");

        (accepted, _server.Published.Count).Should().Be((true, before));
    }

    [Fact]
    public async Task SetCode_while_content_keeps_mode_and_republishes_content()
    {
        await _tv.EnableAsync();
        await _tv.ShowContentAsync("Lugares/Faro Viejo.md");
        var content = _server.Published[^1];

        await _tv.SetCodeAsync("brumavela");

        (_tv.Mode, _server.StartedCode, JsonDocument.Parse(_server.Published[^1]).RootElement.GetProperty("kind").GetString(), _server.Published[^1])
            .Should().Be((TvMode.Content, "brumavela", "content", content));
    }

    [Fact]
    public async Task SetCode_restart_failure_leaves_tv_off_notifies_and_throws()
    {
        await _tv.EnableAsync();
        var changes = 0;
        _tv.Changed += (_, _) => changes++;
        _server.PortsBusy = true;

        var set = () => _tv.SetCodeAsync("brumavela");

        await set.Should().ThrowAsync<IOException>();
        (_tv.Mode, _tv.Endpoint, _tv.Code, _prefs.Get(Key), changes).Should().Be((TvMode.Off, (TvEndpoint?)null, "brumavela", "brumavela", 1));
    }

    [Theory]
    [InlineData("costa-de-sal")]
    [InlineData("COSTA-DE-SAL")]
    public async Task SetCode_with_the_same_code_releases_the_gate(string same)
    {
        await _tv.EnableAsync();
        await _tv.SetCodeAsync(same);

        var next = async () =>
        {
            await _tv.SetCodeAsync("brumavela");
            await _tv.ShowContentAsync("Lugares/Faro Viejo.md");
        };

        await next.Should().CompleteWithinAsync(TimeSpan.FromSeconds(10));
        _tv.Code.Should().Be("brumavela");
    }
}
