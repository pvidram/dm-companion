using System.Text.Json;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class TvSessionTests : IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly TempVault _plain = TempVault.Empty();
    private readonly FakeTvServer _server = new();
    private readonly VaultHost _host;
    private readonly TvSession _session;

    public TvSessionTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        var maps = new MapSession(_host, new InMemoryAppPreferences(), TimeProvider.System, NullLogger<MapSession>.Instance);
        var images = new NoteImageResolver(_host);
        var feed = new TvMapFeed(_host, new TokenHitPoints(_host, GlossaryTestServices.CreateGlossary(_host)), images);
        _session = new TvSession(_server, _host, maps, feed, new TvContentFeed(_host, images, feed), new InMemoryAppPreferences(), NullLogger<TvSession>.Instance);
    }

    Task IAsyncLifetime.InitializeAsync() => _host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
        _plain.Dispose();
    }

    private static (string? Kind, string? Campaign) Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return (root.GetProperty("kind").GetString(), root.GetProperty("campaign").GetString());
    }

    [Fact]
    public async Task Enable_starts_server_and_publishes_waiting_with_campaign_name()
    {
        await _session.EnableAsync();

        (_session.Mode, _server.IsRunning, Parse(_server.Published[^1]))
            .Should().Be((TvMode.Waiting, true, ("waiting", "Costa de Sal")));
    }

    [Fact]
    public async Task Waiting_uses_the_vault_folder_name_without_a_campaign_note()
    {
        await _host.OpenAsync(_plain.Root);

        await _session.EnableAsync();

        Parse(_server.Published[^1]).Campaign.Should().Be(Path.GetFileName(_plain.Root));
    }

    [Fact]
    public async Task Disable_stops_server_and_mode_is_off()
    {
        await _session.EnableAsync();

        await _session.DisableAsync();

        (_session.Mode, _server.IsRunning, _session.Endpoint).Should().Be((TvMode.Off, false, (TvEndpoint?)null));
    }

    [Fact]
    public async Task Ports_busy_propagates_and_stays_off()
    {
        _server.PortsBusy = true;

        var enable = () => _session.EnableAsync();

        await enable.Should().ThrowAsync<IOException>();
        _session.Mode.Should().Be(TvMode.Off);
    }

    [Fact]
    public async Task Connected_and_viewport_follow_the_server_and_raise_changed()
    {
        await _session.EnableAsync();
        var changes = 0;
        _session.Changed += (_, _) => changes++;

        _server.SetConnected(2);
        _server.ReportViewport(new TvViewport(1280, 720));

        (_session.Connected, _session.Viewport, changes).Should().Be((2, new TvViewport(1280, 720), 2));
    }

    [Fact]
    public async Task Address_for_an_ip_includes_port_and_code()
    {
        await _session.EnableAsync();

        _session.AddressFor("192.168.1.20").Should().Be($"http://192.168.1.20:{FakeTvServer.Port}/tv/{_session.Code}/");
    }

    [Fact]
    public void Address_is_null_while_off() => _session.AddressFor("192.168.1.20").Should().BeNull();

    [Fact]
    public async Task Closing_the_app_turns_the_tv_off()
    {
        await _session.EnableAsync();

        await _session.DisableOnExitAsync();

        (_session.Mode, _server.IsRunning).Should().Be((TvMode.Off, false));
    }

    [Fact]
    public async Task Closing_the_app_never_throws_if_the_server_fails_to_stop()
    {
        await _session.EnableAsync();
        _server.StopFailure = new IOException("Fallo al parar.");

        var close = () => _session.DisableOnExitAsync();

        await close.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Switching_vault_while_waiting_publishes_the_new_campaign_name()
    {
        await _session.EnableAsync();

        await _host.OpenAsync(_plain.Root);

        await WaitUntilAsync(() => Parse(_server.Published[^1]).Campaign == Path.GetFileName(_plain.Root));
    }

    [Fact]
    public async Task Vault_changes_that_keep_the_campaign_name_do_not_republish()
    {
        await _session.EnableAsync();
        var published = _server.Published.Count;

        await _host.OpenAsync(_vault.Root);
        await Task.Delay(200);

        _server.Published.Should().HaveCount(published);
    }

    private GlossaryEntry Fireball() => GlossaryTestServices.CreateGlossary(_host).GetSrd("srd:conjuros/fireball")!;

    [Fact]
    public async Task Show_glossary_switches_to_content_mode()
    {
        await _session.EnableAsync();

        await _session.ShowGlossaryAsync(Fireball());

        (_session.Mode, _session.ContentPath, _session.ContentTitle, Parse2(_server.Published[^1])).Should().Be((TvMode.Content, (string?)null, "Bola de fuego", "content"));
    }

    [Fact]
    public async Task Vault_change_keeps_glossary_content_on_tv()
    {
        await _session.EnableAsync();
        await _session.ShowGlossaryAsync(Fireball());
        var published = _server.Published.Count;

        await _host.Current!.SaveAsync("Lugares/Nuevo.md", "---\ntype: location\n---\nNuevo.\n", WritePrecondition.Overwrite);
        await Task.Delay(400);

        (_session.Mode, _session.ContentTitle, _server.Published.Count).Should().Be((TvMode.Content, "Bola de fuego", published));
    }

    [Fact]
    public async Task Switching_vault_returns_tv_to_the_map()
    {
        await _session.EnableAsync();
        await _session.ShowGlossaryAsync(Fireball());

        await _host.OpenAsync(_plain.Root);

        await WaitUntilAsync(() => _session.Mode != TvMode.Content);
        _session.Mode.Should().Be(TvMode.Waiting);
    }

    [Fact]
    public async Task Disable_clears_glossary_content()
    {
        await _session.EnableAsync();
        await _session.ShowGlossaryAsync(Fireball());

        await _session.DisableAsync();

        (_session.Mode, _session.ContentTitle).Should().Be((TvMode.Off, (string?)null));
    }

    private const string ManualPath = "manuales/dnd5e/Manual de prueba.pdf";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task Show_manual_image_switches_to_content()
    {
        await _session.EnableAsync();

        await _session.ShowManualImageAsync(ManualPath, "Manual de prueba · p. 2", Png);

        (_session.Mode, _session.ContentPath, _session.ContentTitle, Parse2(_server.Published[^1]), _server.Files.Count)
            .Should().Be((TvMode.Content, (string?)null, "Manual de prueba · p. 2", "content", 1));
    }

    [Fact]
    public async Task Vault_change_keeps_manual_on_tv()
    {
        await _session.EnableAsync();
        await _session.ShowManualImageAsync(ManualPath, "Manual de prueba · p. 2", Png);
        var published = _server.Published.Count;

        await _host.Current!.SaveAsync("Lugares/Nuevo.md", "---\ntype: location\n---\nNuevo.\n", WritePrecondition.Overwrite);
        await Task.Delay(400);

        (_session.Mode, _session.ContentTitle, _server.Published.Count, _server.Files.Count).Should().Be((TvMode.Content, "Manual de prueba · p. 2", published, 1));
    }

    [Fact]
    public async Task Switching_vault_returns_to_map()
    {
        await _session.EnableAsync();
        await _session.ShowManualImageAsync(ManualPath, "Manual de prueba · p. 2", Png);

        await _host.OpenAsync(_plain.Root);

        await WaitUntilAsync(() => _session.Mode != TvMode.Content);
        (_session.Mode, _server.Files.Count).Should().Be((TvMode.Waiting, 0));
    }

    private static string? Parse2(string json) => JsonDocument.Parse(json).RootElement.GetProperty("kind").GetString();

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
