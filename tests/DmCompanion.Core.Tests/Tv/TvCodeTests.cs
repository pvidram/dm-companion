using DmCompanion.Core.Tv;

namespace DmCompanion.Core.Tests.Tv;

public class TvCodeTests
{
    [Theory]
    [InlineData("brumavela")]
    [InlineData("mesa-2")]
    [InlineData("MESA")]
    [InlineData("  mesa  ")]
    public void Valid_codes_are_accepted(string code) => TvCode.IsValid(code).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("-mesa")]
    [InlineData("mesa-")]
    [InlineData("mesa_1")]
    [InlineData("mésa")]
    [InlineData("mesa uno")]
    public void Invalid_codes_are_rejected(string? code) => TvCode.IsValid(code).Should().BeFalse();

    [Fact]
    public void A_code_of_32_characters_is_valid_and_33_is_not()
    {
        (TvCode.IsValid(new string('a', 32)), TvCode.IsValid(new string('a', 33))).Should().Be((true, false));
    }

    [Fact]
    public void Normalize_lowercases_and_trims() => TvCode.Normalize("  BrumaVela ").Should().Be("brumavela");

    [Theory]
    [InlineData("Campaña Brumavela", "campana-brumavela")]
    [InlineData("  ", "mesa")]
    [InlineData(null, "mesa")]
    [InlineData("Ñandú: ¡Cüento!", "nandu-cuento")]
    [InlineData("luna -- sol", "luna-sol")]
    [InlineData("¡¡!!", "mesa")]
    [InlineData("ab", "mesa")]
    public void Suggest_turns_a_name_into_a_code(string? name, string expected) => TvCode.Suggest(name).Should().Be(expected);

    [Fact]
    public void Suggest_cuts_to_32_and_trims_the_trailing_dash()
    {
        var name = new string('a', 31) + " zzz";

        TvCode.Suggest(name).Should().Be(new string('a', 31));
    }
}
