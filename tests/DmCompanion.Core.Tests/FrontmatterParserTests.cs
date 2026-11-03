using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class FrontmatterParserTests
{
    private const string Valid = "---\ntype: npc\nname: Capitana Ysolde\n---\nCuerpo de la nota.";

    [Fact]
    public void Parse_reads_fields_from_valid_frontmatter() =>
        FrontmatterParser.Parse(Valid).Fields["name"].Should().Be("Capitana Ysolde");

    [Fact]
    public void Parse_returns_body_after_frontmatter() =>
        FrontmatterParser.Parse(Valid).Body.Should().Be("Cuerpo de la nota.");

    [Fact]
    public void Parse_keys_are_case_insensitive() =>
        FrontmatterParser.Parse(Valid).Fields.ContainsKey("NAME").Should().BeTrue();

    [Fact]
    public void Parse_without_frontmatter_returns_whole_content_as_body()
    {
        var result = FrontmatterParser.Parse("Solo texto\n---\nmás");
        (result.Body, result.Fields.Count, result.Error).Should().Be(("Solo texto\n---\nmás", 0, (string?)null));
    }

    [Fact]
    public void Parse_with_empty_frontmatter_returns_no_fields()
    {
        var result = FrontmatterParser.Parse("---\n---\nCuerpo");
        (result.Fields.Count, result.Body, result.HasError).Should().Be((0, "Cuerpo", false));
    }

    [Fact]
    public void Parse_invalid_yaml_reports_error_and_keeps_body()
    {
        var result = FrontmatterParser.Parse("---\nname: [sin cerrar\n---\nCuerpo");
        (result.HasError, result.Body).Should().Be((true, "Cuerpo"));
    }

    [Fact]
    public void Parse_scalar_root_reports_error() =>
        FrontmatterParser.Parse("---\nsolo un texto\n---\nCuerpo").HasError.Should().BeTrue();

    [Fact]
    public void Parse_supports_crlf_line_endings() =>
        FrontmatterParser.Parse("---\r\nname: Varek\r\n---\r\nCuerpo").Fields["name"].Should().Be("Varek");

    [Fact]
    public void Parse_normalises_nested_maps()
    {
        var fields = FrontmatterParser.Parse("---\nstats:\n  ac: 15\n  abilities: { str: 14 }\n---\n").Fields;
        fields["stats"].Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>();
    }

    [Fact]
    public void Parse_null_key_does_not_throw()
    {
        var result = FrontmatterParser.Parse("---\n~: x\n---\nCuerpo");
        result.Body.Should().Be("Cuerpo");
    }

    [Fact]
    public void Parse_normalises_lists()
    {
        var fields = FrontmatterParser.Parse("---\ntags: [puerto, villanos]\n---\n").Fields;
        fields["tags"].Should().BeAssignableTo<IReadOnlyList<object?>>().Which.Should().Equal("puerto", "villanos");
    }
}
