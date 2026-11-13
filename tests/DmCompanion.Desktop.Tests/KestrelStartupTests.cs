using DmCompanion.Screens.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Desktop.Tests;

// Arranca Kestrel de verdad (puerto libre asignado por el SO) para comprobar lo que TestServer no ejecuta.
public sealed class KestrelStartupTests : IDisposable
{
    private readonly TempVault _data = TempVault.Empty();
    private readonly FakeBrowserLauncher _launcher = new();
    private readonly WebApplicationFactory<Program> _factory;

    public KestrelStartupTests()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Desktop:SingleInstance", "true");
            // Puerto libre elegido por el sistema: el test no enlaza el 5190 habitual.
            builder.UseSetting("Desktop:Port", "0");
            builder.UseSetting("Desktop:WaitOnError", "false");
            builder.UseSetting("Desktop:MutexName", @"Local\DmCompanion.Tests." + Guid.NewGuid().ToString("N"));
            builder.UseSetting("Desktop:DataRoot", _data.Root);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAppPreferences, InMemoryAppPreferences>();
                services.AddSingleton<IBrowserLauncher>(_launcher);
            });
        });
        factory.UseKestrel(0);
        _factory = factory;
    }

    public void Dispose()
    {
        _factory.Dispose();
        _data.Dispose();
    }

    private string? StartAndReadUrl()
    {
        using var client = _factory.CreateClient();
        return SingleInstance.ReadUrl(Path.Combine(_data.Root, "Local", "DmCompanion", "instance.json"));
    }

    [Fact]
    public void Startup_writes_instance_json_with_a_url()
    {
        StartAndReadUrl().Should().NotBeNull();
    }

    [Fact]
    public async Task Startup_serves_the_app_at_the_url_in_instance_json()
    {
        using var http = new HttpClient();

        var response = await http.GetAsync(StartAndReadUrl());

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void Startup_opens_the_browser_at_the_url_in_instance_json()
    {
        var url = StartAndReadUrl();

        _launcher.Opened.Should().Equal(url);
    }

    [Fact]
    public void The_log_file_has_no_absolute_paths_after_startup()
    {
        StartAndReadUrl();
        var logs = Path.Combine(_data.Root, "Local", "DmCompanion", "logs");

        var text = Directory.Exists(logs) ? string.Concat(Directory.GetFiles(logs).Select(File.ReadAllText)) : "";

        text.Should().NotContain("Content root path").And.NotContain(_data.Root).And.NotContain(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Startup_does_not_use_the_default_port_when_another_is_configured()
    {
        new Uri(StartAndReadUrl()!).Port.Should().NotBe(5190);
    }
}
