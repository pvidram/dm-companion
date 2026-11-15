using DmCompanion.Core.Rendering;

namespace DmCompanion.Core.Tests.Rendering;

public class NoteImageEmbedTests
{
    private static string Render(string body, Func<string, string?>? resolver, RenderAudience audience = RenderAudience.Dm) =>
        NoteHtmlRenderer.Render(body, audience, _ => true, resolver);

    [Fact]
    public void Embed_becomes_img_with_resolved_url() =>
        Render("![[faro.jpg]]", _ => "/_vault/x/faro.jpg").Should().Contain("<img src=\"/_vault/x/faro.jpg\"").And.Contain("alt=\"faro.jpg\"");

    [Fact]
    public void Embed_with_width_sets_width_attribute() =>
        Render("![[faro.jpg|400]]", _ => "/u").Should().Contain("width=\"400\"");

    [Fact]
    public void Embed_with_non_numeric_width_ignores_it() =>
        Render("![[faro.jpg|grande]]", _ => "/u").Should().NotContain("width=");

    [Fact]
    public void Unresolved_embed_renders_broken_link_text() =>
        Render("![[faro.jpg]]", _ => null).Should().Contain("<span class=\"wikilink broken\">faro.jpg</span>").And.NotContain("<img");

    [Fact]
    public void Embed_inside_secret_block_is_absent_for_players()
    {
        var received = new List<string>();
        var html = Render("Hola\n%%\n![[secreto.png]]\n%%\n", target => { received.Add(target); return "/u"; }, RenderAudience.Players);
        html.Should().NotContain("<img");
        received.Should().BeEmpty();
    }

    [Fact]
    public void Embed_url_is_attribute_encoded() =>
        Render("![[faro.jpg]]", _ => "/a\"onerror=\"x").Should().Contain("src=\"/a&quot;onerror=&quot;x\"");

    [Fact]
    public void Embed_without_resolver_renders_broken_link() =>
        Render("![[faro.jpg]]", null).Should().Contain("wikilink broken");

    [Fact]
    public void Forged_out_of_range_marker_renders_without_exception() =>
        Render("a \uE002999\uE003 b \uE00299999999999999999999\uE003", _ => "/u").Should().NotContain("<img");

    [Fact]
    public void Forged_marker_inside_link_destination_does_not_produce_img() =>
        Render("![[faro.jpg]] [x](http://a/\uE0020\uE003)", _ => "/u").Should().NotContain("href=\"http://a/<img");

    [Fact]
    public void Forged_secret_markers_are_removed_for_players() =>
        Render("\uE000hola\uE001", null, RenderAudience.Players).Should().NotContain("dm-secret");

    [Theory]
    [InlineData("`![[faro.jpg]]`")]
    [InlineData("``a ![[faro.jpg]] b``")]
    [InlineData("```\n![[faro.jpg]]\n```")]
    [InlineData("~~~md\n![[faro.jpg]]\n~~~")]
    [InlineData("Texto\n\n    ![[faro.jpg]]\n")]
    [InlineData("`a\n![[faro.jpg]]`")]
    [InlineData("- lista\n\n      ![[faro.jpg]]\n")]
    public void Embed_inside_code_stays_literal_text(string body) =>
        Render(body, _ => "/u").Should().NotContain("<img").And.Contain("![[faro.jpg]]");

    [Theory]
    [InlineData("[x](http://a \"![[faro.jpg]]\")")]
    [InlineData("[x](http://a '![[faro.jpg]]')")]
    [InlineData("![![[faro.jpg]]](http://a/b.png)")]
    [InlineData("[x]\n\n[x]: http://a \"![[faro.jpg]]\"")]
    public void Embed_inside_an_attribute_produces_no_html(string body)
    {
        var html = Render(body, _ => "/u");

        (html.Contains("<img src=\"/u\""), html.Contains("&lt;img"), html.Contains('\uE002')).Should().Be((false, false, false));
    }

    [Fact]
    public void Embed_that_is_not_drawn_as_an_image_is_never_resolved()
    {
        var received = new List<string>();

        Render("    ![[codigo.png]]\n\n[x](http://a \"![[titulo.png]]\") ![[visible.png]]", target => { received.Add(target); return "/u"; });

        received.Should().Equal("visible.png");
    }
}
