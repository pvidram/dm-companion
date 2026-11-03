using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class FrontmatterReaderTests
{
    private static IReadOnlyDictionary<string, object?> Fields(string yaml) =>
        FrontmatterParser.Parse($"---\n{yaml}\n---\n").Fields;

    [Fact]
    public void GetString_trims_value() =>
        FrontmatterReader.GetString(Fields("name: '  Varek  '"), "name").Should().Be("Varek");

    [Fact]
    public void GetString_returns_null_for_empty_value() =>
        FrontmatterReader.GetString(Fields("portrait:"), "portrait").Should().BeNull();

    [Fact]
    public void GetInt_parses_integers() =>
        FrontmatterReader.GetInt(Fields("hp: 52"), "hp").Should().Be(52);

    [Fact]
    public void GetInt_returns_null_for_non_numbers() =>
        FrontmatterReader.GetInt(Fields("hp: mucho"), "hp").Should().BeNull();

    [Theory]
    [InlineData("secret: true", true)]
    [InlineData("secret: True", true)]
    [InlineData("secret: false", false)]
    [InlineData("otra: 1", false)]
    public void GetBool_reads_booleans(string yaml, bool expected) =>
        FrontmatterReader.GetBool(Fields(yaml), "secret").Should().Be(expected);

    [Fact]
    public void GetMap_reads_nested_values()
    {
        var stats = FrontmatterReader.GetMap(Fields("stats:\n  ac: 15"), "stats");
        FrontmatterReader.GetInt(stats, "ac").Should().Be(15);
    }

    [Fact]
    public void GetMap_returns_empty_when_missing() =>
        FrontmatterReader.GetMap(Fields("name: x"), "stats").Should().BeEmpty();
}
