using System.Text.RegularExpressions;
using DmCompanion.Core.Rendering;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class NoteHtmlRendererTests
{
    private static string Dm(string body, Func<string, bool>? exists = null) =>
        NoteHtmlRenderer.Render(body, RenderAudience.Dm, exists ?? (_ => true));

    private static string Players(string body) =>
        NoteHtmlRenderer.Render(body, RenderAudience.Players, _ => true);

    [Fact]
    public void Render_converts_markdown() => Dm("## Muelles").Should().Contain("<h2").And.Contain("Muelles</h2>");

    [Fact]
    public void Render_turns_wikilinks_into_anchors() =>
        Dm("Ve a [[Puerto Sal]]").Should().Contain("<a href=\"#\" class=\"wikilink\" data-target=\"Puerto Sal\">Puerto Sal</a>");

    [Fact]
    public void Render_marks_broken_wikilinks() =>
        Dm("[[Nada]]", _ => false).Should().Contain("class=\"wikilink broken\"");

    [Fact]
    public void Render_shows_alias_as_link_text() =>
        Dm("[[Varek|el tuerto]]").Should().Contain(">el tuerto</a>");

    [Fact]
    public void Render_handles_accented_targets() =>
        Dm("[[Sesión 1]]").Should().Contain("data-target=\"Sesión 1\"");

    [Fact]
    public void Render_highlights_secrets_for_dm() =>
        Dm("Hola %%es la hija del Kraken%% fin").Should().Contain("<span class=\"dm-secret\">🔒 es la hija del Kraken</span>");

    [Fact]
    public void Render_removes_secrets_for_players() =>
        Players("Hola %%es la hija del Kraken%% fin").Should().NotContain("Kraken");

    [Fact]
    public void Render_escapes_raw_html() => Dm("<script>alert(1)</script>").Should().NotContain("<script>");

    [Fact]
    public void Render_neutralises_javascript_links() =>
        Dm("[pulsa](javascript:alert(1))").Should().NotContain("javascript:");

    [Fact]
    public void Render_keeps_http_links() =>
        Dm("[SRD](https://example.com)").Should().Contain("href=\"https://example.com\"");

    [Fact]
    public void Render_leaves_wikilinks_in_code_untouched() =>
        Dm("`[[Nota]]`").Should().Contain("<code>[[Nota]]</code>");

    [Fact]
    public void Render_drops_generic_attributes_on_blocks() =>
        Dm("# T {onclick=\"alert(1)\"}").Should().NotContain("onclick=\"");

    [Fact]
    public void Render_drops_generic_attributes_on_images() =>
        Dm("![x](a.png){onerror=\"alert(2)\"}").Should().NotContain("onerror=\"");

    [Fact]
    public void Render_neutralises_javascript_image_sources() =>
        Dm("![y](javascript:alert(3))").Should().NotContain("javascript:");

    [Fact]
    public void Render_keeps_https_image_sources() =>
        Dm("![m](https://example.com/m.png)").Should().Contain("src=\"https://example.com/m.png\"");

    [Fact]
    public void Render_keeps_multiparagraph_secret_text_for_dm() =>
        Dm("A\n%%\nlínea uno\n\nlínea dos\n%%\nB").Should().Contain("línea uno").And.Contain("línea dos");

    [Fact]
    public void Render_balances_spans_in_multiparagraph_secrets()
    {
        var html = Dm("A\n%%\nlínea uno\n\nlínea dos\n%%\nB");
        Regex.Matches(html, "<span class=\"dm-secret\">").Count.Should().Be(Regex.Matches(html, "</span>").Count);
    }

    [Fact]
    public void Render_uses_target_without_section_in_data_target() =>
        Dm("[[Puerto Sal#Muelles]]").Should().Contain("data-target=\"Puerto Sal\"");

    [Theory]
    [InlineData("a \\` %% LEAK1 %% `", "LEAK1")]
    [InlineData("- item\n  ```\n  code\n\nfuera %% LEAK2 %%", "LEAK2")]
    [InlineData("1. uno\n   ```\n   c\n2. dos %% LEAK10 %%", "LEAK10")]
    [InlineData("Público %% s1 `%%` LEAKPAIR %% público2", "LEAKPAIR")]
    public void Render_for_players_never_leaks_secrets_when_masker_and_markdig_disagree(string body, string leak) =>
        Players(body).Should().NotContain(leak);

    [Fact]
    public void RenderForPlayers_returns_empty_for_secret_entity() =>
        NoteHtmlRenderer.RenderForPlayers(VaultEntityFactory.Create("a.md", "---\nsecret: true\n---\nTexto", DateTime.UtcNow), _ => true)
            .Should().BeEmpty();

    [Fact]
    public void RenderForPlayers_renders_public_entity() =>
        NoteHtmlRenderer.RenderForPlayers(VaultEntityFactory.Create("a.md", "Texto %%x%%", DateTime.UtcNow), _ => true)
            .Should().Contain("Texto").And.NotContain("x%%");

    [Fact]
    public void Render_for_players_keeps_public_text_before_mirrored_secret() =>
        Players("Público %% s1 `%%` LEAKPAIR %% público2").Should().Contain("Público");
}
