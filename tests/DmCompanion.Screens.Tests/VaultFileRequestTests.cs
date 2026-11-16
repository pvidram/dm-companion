using DmCompanion.Core.Vault;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class VaultFileRequestTests
{
    private static readonly DateTime Stamp = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    private static async Task<VaultHost> OpenHostAsync(TempVault vault)
    {
        var host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        (await host.OpenAsync(vault.Root)).Should().BeTrue();
        return host;
    }

    [Fact]
    public async Task Url_encodes_each_segment_and_roundtrips_through_TryParse()
    {
        using var vault = TempVault.FromDemo();
        await using var host = await OpenHostAsync(vault);
        var urls = new VaultFileUrls(host);

        var url = urls.UrlFor("Mapas/Campaña del Faro.png", Stamp);

        url.Should().Be($"/_vault/Mapas/Campa%C3%B1a%20del%20Faro.png?v={Stamp.Ticks}");
        VaultFileRequest.TryParse(url!, out var relative).Should().BeTrue();
        relative.Should().Be("Mapas/Campaña del Faro.png");
    }

    [Theory]
    [InlineData("/_vault/_audio/Taberna/a%20b.mp3?v=1", "_audio/Taberna/a b.mp3")]
    [InlineData("/_vault/_audio/x.FLAC", "_audio/x.FLAC")]
    public void TryParse_accepts_audio(string url, string expected)
    {
        VaultFileRequest.TryParse(url, out var relative).Should().BeTrue();
        relative.Should().Be(expected);
    }

    [Theory]
    [InlineData("/_vault/..%2fx.mp3")]
    [InlineData("/_vault/_audio/%2e%2e/x.mp3")]
    [InlineData("/_vault/_audio%5c..%5cx.mp3")]
    [InlineData("/_vault/_audio/a.mp3.txt")]
    public void TryParse_rejects_audio_traversal(string url) => VaultFileRequest.TryParse(url, out _).Should().BeFalse();

    [Fact]
    public void TryParse_rejects_other_files() => VaultFileRequest.TryParse("/_vault/_audio/x.json", out _).Should().BeFalse();

    [Fact]
    public void ContentTypeOf_knows_audio() => VaultFileRequest.ContentTypeOf("_audio/x.mp3").Should().Be("audio/mpeg");

    [Fact]
    public async Task Leading_slash_is_trimmed_not_rejected()
    {
        using var vault = TempVault.FromDemo();
        await using var host = await OpenHostAsync(vault);
        new VaultFileUrls(host).UrlFor("/Mapas/x.png", Stamp).Should().Be($"/_vault/Mapas/x.png?v={Stamp.Ticks}");
    }

    [Theory]
    [InlineData("../x.png")]
    [InlineData("/../x.png")]
    [InlineData("C:/x.png")]
    [InlineData("a/.oculta/x.png")]
    [InlineData("")]
    public async Task Url_is_null_for_unsafe_paths(string path)
    {
        using var vault = TempVault.FromDemo();
        await using var host = await OpenHostAsync(vault);
        new VaultFileUrls(host).UrlFor(path, Stamp).Should().BeNull();
    }

    [Fact]
    public void Url_is_null_without_an_open_vault()
    {
        var host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        new VaultFileUrls(host).UrlFor("Mapas/a.png", Stamp).Should().BeNull();
    }

    [Theory]
    [InlineData("/_vault/%2e%2e/x.png")]
    [InlineData("/_vault/..%2fx.png")]
    [InlineData("/_vault/Mapas%5c..%5c..%5cx.png")]
    [InlineData("/_vault/..%5cx.png")]
    [InlineData("/_vault/%2e%2e%2fx.png")]
    [InlineData("/_vault/x.svg")]
    [InlineData("/_vault/x.md")]
    [InlineData("/_vault/")]
    [InlineData("/_vault")]
    [InlineData("/otra/x.png")]
    [InlineData("/_vault/.oculta/x.png")]
    [InlineData("/_vault/C%3A/x.png")]
    [InlineData("/_vault/x%00.png")]
    [InlineData("/_vault//x.png")]
    public void TryParse_rejects_unsafe_or_non_image_requests(string url) =>
        VaultFileRequest.TryParse(url, out _).Should().BeFalse();

    [Fact]
    public void TryParse_decodes_only_once()
    {
        // %2541 decodifica una vez a "%41", que es un nombre literal, no una «A».
        VaultFileRequest.TryParse("/_vault/Mapas/%2541.png", out var relative).Should().BeTrue();
        relative.Should().Be("Mapas/%41.png");
    }

    [Theory]
    [InlineData("a.png", "image/png")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("manuales/dnd/a.PDF", "application/pdf")]
    public void ContentType_follows_the_extension(string path, string expected) =>
        VaultFileRequest.ContentTypeOf(path).Should().Be(expected);

    [Theory]
    [InlineData("/_vault/Notas/secreto.pdf")]
    [InlineData("/_vault/x.pdf")]
    [InlineData("/_vault/manualesx/a.pdf")]
    public void TryParse_rejects_pdf_outside_manuals(string url) =>
        VaultFileRequest.TryParse(url, out _).Should().BeFalse();

    [Fact]
    public void TryParse_accepts_pdf_under_manuals_regardless_of_case() =>
        VaultFileRequest.TryParse("/_vault/Manuales/dnd5e/a.pdf", out _).Should().BeTrue();

    [Fact]
    public void TryParse_accepts_pdf_manuals()
    {
        VaultFileRequest.TryParse("/_vault/manuales/dnd/Manual%20del%20jugador.pdf", out var relative).Should().BeTrue();
        relative.Should().Be("manuales/dnd/Manual del jugador.pdf");
    }
}
