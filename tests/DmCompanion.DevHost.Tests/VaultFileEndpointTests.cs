using System.Net;
using DmCompanion.Screens.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.DevHost.Tests;

public sealed class VaultFileEndpointTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly WebApplicationFactory<Program> _factory;

    public VaultFileEndpointTests()
    {
        Directory.CreateDirectory(_vault.PathOf("Mapas"));
        File.WriteAllBytes(_vault.PathOf("Mapas/Campaña del Faro.png"), Png);
        File.WriteAllText(_vault.PathOf("Mapas/x.svg"), "<svg/>");
        Directory.CreateDirectory(_vault.PathOf("_audio"));
        File.WriteAllBytes(_vault.PathOf("_audio/a.mp3"), Enumerable.Range(0, 100).Select(i => (byte)i).ToArray());
        Directory.CreateDirectory(_vault.PathOf("manuales/dnd"));
        File.WriteAllBytes(_vault.PathOf("manuales/dnd/phb.pdf"), Enumerable.Range(0, 300).Select(i => (byte)i).ToArray());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DevHost:VaultPath", _vault.Root);
            builder.ConfigureTestServices(services => services.AddSingleton<IAppPreferences, InMemoryAppPreferences>());
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        _vault.Dispose();
    }

    [Fact]
    public async Task Existing_png_is_served_with_its_content_type_and_bytes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_vault/Mapas/Campa%C3%B1a%20del%20Faro.png?v=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
    }

    [Fact]
    public async Task Audio_is_served_with_range_support()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_vault/_audio/a.mp3");
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 9);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Enumerable.Range(0, 10).Select(i => (byte)i));
    }

    [Fact]
    public async Task Pdf_range_request_returns_206_with_content_range()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_vault/manuales/dnd/phb.pdf");
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 99);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentRange!.ToString().Should().Be("bytes 0-99/300");
    }

    [Theory]
    [InlineData("/_vault/%2e%2e/x.png")]
    [InlineData("/_vault/..%2fx.png")]
    [InlineData("/_vault/Mapas%5c..%5c..%5cx.png")]
    [InlineData("/_vault/Mapas/x.svg")]
    [InlineData("/_vault/")]
    [InlineData("/_vault/Mapas/NoExiste.png")]
    [InlineData("/_vault/NoExiste/NoExiste.png")]
    public async Task Rejected_or_missing_files_are_404(string url)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
