using DmCompanion.Core.Glossary;

namespace DmCompanion.Core.Tests.Glossary;

public sealed class GlossaryHtmlRendererTests
{
    private static GlossarySpan[] T(string text, SpanStyle style = SpanStyle.None) => [new(text, style)];
    private static GlossaryBlock P(string text) => new(BlockKind.Paragraph, T(text));
    private static GlossaryBlock Item(string text) => new(BlockKind.ListItem, T(text));

    [Fact]
    public void Text_is_encoded()
    {
        var html = GlossaryHtmlRenderer.Render([P("<script>alert(1)</script>")]);
        html.Should().Contain("&lt;script&gt;").And.NotContain("<script");
    }

    [Fact]
    public void Bold_and_italic()
        => GlossaryHtmlRenderer.Render([new GlossarySpan("a", SpanStyle.Bold), new GlossarySpan("b", SpanStyle.Italic)])
            .Should().Be("<strong>a</strong><em>b</em>");

    [Fact]
    public void Consecutive_list_items_share_one_list()
    {
        var html = GlossaryHtmlRenderer.Render([Item("a"), Item("b"), P("c")]);
        html.Should().Be("<ul><li>a</li><li>b</li></ul><p>c</p>");
    }

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<GlossarySpan>>> Rows() =>
        [[T("H1"), T("H2")], [T("a"), T("b")]];

    [Fact]
    public void Table_has_header_unless_vertical()
    {
        var normal = GlossaryHtmlRenderer.Render([new GlossaryBlock(BlockKind.Table, Rows: Rows(), Title: "T")]);
        var vertical = GlossaryHtmlRenderer.Render([new GlossaryBlock(BlockKind.Table, Rows: Rows(), Vertical: true)]);
        (normal.Contains("<thead>") && normal.Contains("<caption>T</caption>"), vertical.Contains("<thead>")).Should().Be((true, false));
    }

    [Fact]
    public void Box_wraps_children()
        => GlossaryHtmlRenderer.Render([new GlossaryBlock(BlockKind.Box, Title: "Nota", Children: [P("x")])])
            .Should().Be("<aside class=\"ds-glossary-box\"><h4>Nota</h4><p>x</p></aside>");

    [Fact]
    public void Quotes_are_encoded()
        => GlossaryHtmlRenderer.Render(T("\"a\" 'b'")).Should().NotContain("\"").And.NotContain("'");
}
