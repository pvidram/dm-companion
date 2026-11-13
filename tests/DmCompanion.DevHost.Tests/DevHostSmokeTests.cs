using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.DevHost.Tests;

public sealed class DevHostSmokeTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly WebApplicationFactory<Program> _factory;

    public DevHostSmokeTests() =>
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DevHost:VaultPath", _vault.Root);
            builder.ConfigureTestServices(services => services.AddSingleton<IAppPreferences, InMemoryAppPreferences>());
        });

    public void Dispose()
    {
        _factory.Dispose();
        _vault.Dispose();
    }

    [Fact]
    public async Task Diagnostics_route_renders_the_note_count_of_the_demo_vault()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/diag");

        html.Should().Contain("Notas: 11");
    }

    [Fact]
    public async Task Home_renders_the_shell_rail_when_the_vault_is_open()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.Should().Contain("Cambiar vault").And.Contain("Jugadores").And.Contain("Notas");
    }

    [Fact]
    public async Task Styleguide_route_responds_ok()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/styleguide");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public void ShellState_is_independent_between_scopes()
    {
        using var first = _factory.Services.CreateScope();
        using var second = _factory.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<ShellState>();
        var b = second.ServiceProvider.GetRequiredService<ShellState>();

        a.Toggle(OverlayPanel.Players);

        b.Should().NotBeSameAs(a);
        b.ActivePanel.Should().Be(OverlayPanel.None);
    }

    [Fact]
    public async Task Host_stylesheet_gives_the_page_full_height()
    {
        using var client = _factory.CreateClient();

        var css = await client.GetStringAsync("/app.css");

        css.Should().Contain("height: 100%");
    }
}

internal sealed class InMemoryAppPreferences : IAppPreferences
{
    private readonly Dictionary<string, string> _values = [];

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string value) => _values[key] = value;

    public void Remove(string key) => _values.Remove(key);
}
