using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class WikilinkExtractorTests
{
    [Fact]
    public void Extract_reads_simple_link() =>
        WikilinkExtractor.Extract("Ve a [[Puerto Sal]].").Should().ContainSingle()
            .Which.Should().Be(new Wikilink("Puerto Sal", null, null, false));

    [Fact]
    public void Extract_reads_alias() =>
        WikilinkExtractor.Extract("[[Capitana Ysolde|la capitana]]").Single().Alias.Should().Be("la capitana");

    [Fact]
    public void Extract_reads_section() =>
        WikilinkExtractor.Extract("[[Puerto Sal#Muelles]]").Single().Should().Be(new Wikilink("Puerto Sal", "Muelles", null, false));

    [Fact]
    public void Extract_reads_section_and_alias() =>
        WikilinkExtractor.Extract("[[Puerto Sal#Muelles|los muelles]]").Single()
            .Should().Be(new Wikilink("Puerto Sal", "Muelles", "los muelles", false));

    [Fact]
    public void Extract_marks_embeds() =>
        WikilinkExtractor.Extract("![[mapa.png]]").Single().IsEmbed.Should().BeTrue();

    [Fact]
    public void Extract_strips_escaped_pipe_used_in_tables() =>
        WikilinkExtractor.Extract(@"| [[Varek\|el prestamista]] |").Single().Target.Should().Be("Varek");

    [Fact]
    public void Extract_ignores_links_inside_fenced_code() =>
        WikilinkExtractor.Extract("```\n[[No es enlace]]\n```").Should().BeEmpty();

    [Fact]
    public void Extract_ignores_links_inside_inline_code() =>
        WikilinkExtractor.Extract("usa `[[Nota]]` así").Should().BeEmpty();

    [Fact]
    public void Extract_ignores_self_section_links() =>
        WikilinkExtractor.Extract("[[#Sección]]").Should().BeEmpty();

    [Fact]
    public void Extract_returns_links_in_order() =>
        WikilinkExtractor.Extract("[[A]] y [[B]]").Select(l => l.Target).Should().Equal("A", "B");

    [Fact]
    public void FindMatches_reports_position_and_length() =>
        WikilinkExtractor.FindMatches("Ve a [[Puerto Sal]].").Single()
            .Should().Match<WikilinkMatch>(m => m.Index == 5 && m.Length == 14);

    [Theory]
    [InlineData("[[Varek]]", "Varek")]
    [InlineData("[[Varek|el tuerto]]", "el tuerto")]
    [InlineData("[[Puerto Sal#Muelles]]", "Puerto Sal › Muelles")]
    public void DisplayText_prefers_alias_then_target_and_section(string text, string expected) =>
        WikilinkExtractor.Extract(text).Single().DisplayText.Should().Be(expected);
}
