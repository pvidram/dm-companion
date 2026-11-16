using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class EmbeddedTvAssetsTests
{
    private readonly EmbeddedTvAssets _assets = new();

    [Theory]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("tv.css", "text/css; charset=utf-8")]
    [InlineData("tv-app.js", "text/javascript; charset=utf-8")]
    [InlineData("tv-layout.js", "text/javascript; charset=utf-8")]
    [InlineData("tv-strip.js", "text/javascript; charset=utf-8")]
    [InlineData("tv-map.js", "text/javascript; charset=utf-8")]
    [InlineData("tv-strip.css", "text/css; charset=utf-8")]
    [InlineData("token-opacity.js", "text/javascript; charset=utf-8")]
    [InlineData("konva.min.js", "text/javascript; charset=utf-8")]
    [InlineData("eb-garamond-latin-600-normal.woff2", "font/woff2")]
    [InlineData("source-sans-3-latin-400-normal.woff2", "font/woff2")]
    public void Known_assets_are_embedded_with_their_content_type(string name, string contentType)
    {
        using var stream = _assets.Open(name);

        (stream is { Length: > 0 }, _assets.ContentType(name)).Should().Be((true, contentType));
    }

    [Theory]
    [InlineData("map-canvas.js")]
    [InlineData("../index.html")]
    [InlineData("DmCompanion.Screens.dll")]
    public void Unknown_names_are_null(string name) => _assets.Open(name).Should().BeNull();

    [Fact]
    public void Index_loads_only_its_own_assets()
    {
        using var reader = new StreamReader(_assets.Open("index.html")!);
        var html = reader.ReadToEnd();

        (html.Contains("assets/tv.css"), html.Contains("assets/konva.min.js"), html.Contains("assets/tv-app.js"), html.Contains("http"))
            .Should().Be((true, true, true, false));
    }
}
