using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.GameSystems;

public class FrontmatterPathTests
{
    private static IReadOnlyDictionary<string, object?> Fields(string yaml) =>
        FrontmatterParser.Parse($"---\n{yaml}\n---\n").Fields;

    private const string Yaml = "stats:\n  ac: 15\n  skills:\n    stealth: 5";

    [Fact]
    public void GetPath_returns_nested_value() =>
        FrontmatterReader.GetPath(Fields(Yaml), "stats.skills.stealth").Should().Be("5");

    [Fact]
    public void GetPath_returns_null_for_missing_segment() =>
        FrontmatterReader.GetPath(Fields(Yaml), "stats.skills.arcana").Should().BeNull();

    [Fact]
    public void GetPath_returns_null_when_intermediate_is_not_a_map() =>
        FrontmatterReader.GetPath(Fields(Yaml), "stats.ac.value").Should().BeNull();
}
