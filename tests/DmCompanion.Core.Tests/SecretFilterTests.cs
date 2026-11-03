using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class SecretFilterTests
{
    [Fact]
    public void RemoveSecretBlocks_removes_inline_comment() =>
        SecretFilter.RemoveSecretBlocks("Hola %%secreto%% fin").Should().Be("Hola  fin");

    [Fact]
    public void RemoveSecretBlocks_removes_multiline_comment() =>
        SecretFilter.RemoveSecretBlocks("A\n%%\nlínea 1\nlínea 2\n%%\nB").Should().Be("A\n\nB");

    [Fact]
    public void RemoveSecretBlocks_removes_several_comments() =>
        SecretFilter.RemoveSecretBlocks("%%a%%x%%b%%").Should().Be("x");

    [Fact]
    public void RemoveSecretBlocks_hides_until_end_when_not_closed() =>
        SecretFilter.RemoveSecretBlocks("Visible %%oculto para siempre").Should().Be("Visible ");

    [Fact]
    public void RemoveSecretBlocks_keeps_percent_signs_inside_code() =>
        SecretFilter.RemoveSecretBlocks("`%%no%%`").Should().Be("`%%no%%`");

    [Fact]
    public void FindSecretRanges_includes_delimiters() =>
        SecretFilter.FindSecretRanges("ab%%cd%%ef").Should().Equal(new TextRange(2, 6));

    [Fact]
    public void RemoveSecretBlocks_hides_secret_after_inline_triple_backtick_line() =>
        SecretFilter.RemoveSecretBlocks("```foo```\n%%secreto%%").Should().NotContain("secreto");

    [Theory]
    [InlineData("secret: yes", true)]
    [InlineData("secret: on", true)]
    [InlineData("secret: no", false)]
    [InlineData("secret: off", false)]
    [InlineData("secret: true", true)]
    [InlineData("secret: false", false)]
    [InlineData("name: Varek", false)]
    public void IsSecret_reads_frontmatter_flag(string yaml, bool expected) =>
        SecretFilter.IsSecret(FrontmatterParser.Parse($"---\n{yaml}\n---\n").Fields).Should().Be(expected);

    [Fact]
    public void RemoveSecretBlocksForPlayers_removes_percent_blocks_inside_code() =>
        SecretFilter.RemoveSecretBlocksForPlayers("`%%no%%` fin").Should().Be("`` fin");

    [Fact]
    public void RemoveSecretBlocksForPlayers_removes_ordinary_blocks() =>
        SecretFilter.RemoveSecretBlocksForPlayers("Hola %%x%% fin").Should().Be("Hola  fin");

    [Theory]
    [InlineData("secret: [true]")]
    [InlineData("secret: !!bool true")]
    [InlineData("secret: { a: 1 }")]
    public void IsSecret_treats_non_string_values_as_secret(string yaml) =>
        SecretFilter.IsSecret(FrontmatterParser.Parse($"---\n{yaml}\n---\n").Fields).Should().BeTrue();

    [Fact]
    public void RemoveSecretBlocksForPlayers_removes_union_of_masked_and_unmasked_ranges() =>
        SecretFilter.RemoveSecretBlocksForPlayers("P %% s1 `%%` LEAKPAIR %% q").Should().NotContain("LEAKPAIR");
}
