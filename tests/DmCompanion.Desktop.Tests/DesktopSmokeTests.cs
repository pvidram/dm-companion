using System.Net;
using System.Text.RegularExpressions;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Desktop.Tests;

public sealed class DesktopSmokeTests : IDisposable
{
    private readonly TempVault _data = TempVault.Empty();
    private readonly TempVault _documents = TempVault.Empty();
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FakeBrowserLauncher _launcher = new();
    private readonly WebApplicationFactory<Program> _factory;

    public DesktopSmokeTests() =>
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Desktop:SingleInstance", "false");
            builder.UseSetting("Desktop:DataRoot", _data.Root);
            builder.UseSetting("Desktop:DocumentsRoot", _documents.Root);
            builder.UseSetting("Desktop:SampleSource", TempVault.FindDemo());
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAppPreferences, InMemoryAppPreferences>();
                services.AddSingleton<IBrowserLauncher>(_launcher);
                services.AddSingleton<ILogFolder>(new FakeLogFolder());
            });
        });

    public void Dispose()
    {
        _factory.Dispose();
        _vault.Dispose();
        _documents.Dispose();
        _data.Dispose();
    }

    [Fact]
    public async Task Home_renders_the_welcome_screen_when_no_vault_is_saved()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.Should().Contain("Bienvenido a DM Companion");
    }

    [Fact]
    public async Task Blazor_script_is_served_to_the_window()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_framework/blazor.web.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Every_stylesheet_the_home_page_links_is_served()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");

        var hrefs = Regex.Matches(html, "<link[^>]*rel=\"stylesheet\"[^>]*href=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value).ToList();
        var failed = new List<string>();
        foreach (var href in hrefs)
        {
            var response = await client.GetAsync(href);
            if (response.StatusCode != HttpStatusCode.OK) failed.Add($"{href} -> {(int)response.StatusCode}");
        }

        failed.Should().BeEmpty();
    }

    [Fact]
    public async Task Desktop_endpoints_do_not_exist_outside_desktop_mode()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_desktop/focus");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Styleguide_route_is_not_found()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/styleguide");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_with_a_foreign_host_is_rejected()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "evil.example";

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Request_with_the_localhost_host_is_accepted()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Vault_endpoint_rejects_path_traversal_with_an_open_vault()
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_vault.Root)!, "secret.txt"), "secreto");
        await _factory.Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_vault/%2e%2e/secret.txt");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Diagnostics_offers_to_open_the_logs_folder()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/diag");

        html.Should().Contain("Abrir carpeta de registros");
    }
}
