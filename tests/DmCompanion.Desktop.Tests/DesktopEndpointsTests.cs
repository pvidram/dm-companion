using System.Net;
using System.Text;
using DmCompanion.Desktop.Window;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Desktop.Tests;

public sealed class DesktopEndpointsTests : IAsyncLifetime
{
    private const string RemoteHeader = "X-Test-Remote";
    private readonly FakeBrowserLauncher _launcher = new();
    private readonly FakeWindowActivator _window = new();
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IBrowserLauncher>(_launcher);
        builder.Services.AddSingleton<IWindowActivator>(_window);
        _app = builder.Build();
        // TestServer no fija la IP remota: este middleware la toma de una cabecera de prueba.
        _app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[RemoteHeader].ToString());
            return next(context);
        });
        _app.MapDesktopEndpoints();
        await _app.StartAsync();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private async Task<HttpResponseMessage> SendAsync(string method, string path, IPAddress remote, string? json = null, string? origin = null, string contentType = "application/json")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Host = "127.0.0.1:5190";
        request.Headers.Add(RemoteHeader, remote.ToString());
        if (origin is not null) request.Headers.Add("Origin", origin);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, contentType);
        return await _app.GetTestClient().SendAsync(request);
    }

    [Fact]
    public async Task Focus_from_loopback_brings_the_window_to_front()
    {
        using var response = await SendAsync("GET", DesktopEndpoints.FocusPath, IPAddress.Loopback);

        ((int)response.StatusCode, _window.Calls).Should().Be((204, 1));
    }

    [Fact]
    public async Task Focus_from_another_address_is_forbidden()
    {
        using var response = await SendAsync("GET", DesktopEndpoints.FocusPath, IPAddress.Parse("192.168.1.20"));

        ((int)response.StatusCode, _window.Calls).Should().Be((403, 0));
    }

    [Fact]
    public async Task Open_external_opens_an_https_url()
    {
        using var response = await SendAsync("POST", DesktopEndpoints.OpenExternalPath, IPAddress.Loopback, """{"url":"https://example.test/a"}""");

        ((int)response.StatusCode, _launcher.Opened.Count).Should().Be((204, 1));
    }

    [Theory]
    [InlineData("""{"url":"file:///etc/passwd"}""")]
    [InlineData("""{"url":"javascript:alert(1)"}""")]
    [InlineData("""{"url":"no es una url"}""")]
    [InlineData("""{}""")]
    public async Task Open_external_rejects_non_web_urls(string body)
    {
        using var response = await SendAsync("POST", DesktopEndpoints.OpenExternalPath, IPAddress.Loopback, body);

        ((int)response.StatusCode, _launcher.Opened.Count).Should().Be((400, 0));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/x-www-form-urlencoded")]
    public async Task Open_external_rejects_a_non_json_content_type(string contentType)
    {
        using var response = await SendAsync("POST", DesktopEndpoints.OpenExternalPath, IPAddress.Loopback,
            """{"url":"https://example.test/a"}""", contentType: contentType);

        ((int)response.StatusCode, _launcher.Opened.Count).Should().Be((415, 0));
    }

    [Fact]
    public async Task Open_external_accepts_the_apps_own_origin()
    {
        using var response = await SendAsync("POST", DesktopEndpoints.OpenExternalPath, IPAddress.Loopback,
            """{"url":"https://example.test/a"}""", origin: "http://127.0.0.1:5190");

        ((int)response.StatusCode, _launcher.Opened.Count).Should().Be((204, 1));
    }

    [Fact]
    public async Task Open_external_rejects_a_foreign_origin()
    {
        using var response = await SendAsync("POST", DesktopEndpoints.OpenExternalPath, IPAddress.Loopback,
            """{"url":"https://example.test/a"}""", origin: "https://evil.test");

        ((int)response.StatusCode, _launcher.Opened.Count).Should().Be((403, 0));
    }

    [Fact]
    public async Task Open_external_from_another_address_is_forbidden()
    {
        using var response = await SendAsync("POST", DesktopEndpoints.OpenExternalPath, IPAddress.Parse("10.0.0.5"),
            """{"url":"https://example.test/a"}""");

        ((int)response.StatusCode).Should().Be(403);
    }
}
