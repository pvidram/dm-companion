using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class FolderNameTests
{
    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData("a/b")] [InlineData("a\\b")] [InlineData("a:b")]
    [InlineData("a*b")] [InlineData("a?b")] [InlineData("a\"b")] [InlineData("a<b")] [InlineData("a>b")]
    [InlineData("a|b")] [InlineData(".")] [InlineData("..")] [InlineData(".oculta")] [InlineData("CON")]
    [InlineData("nul")] [InlineData("Com1")] [InlineData("aux.txt")] [InlineData("termina.")] [InlineData("con\ttab")] [InlineData("a\0b")]
    public void Check_rejects_invalid_names(string raw) =>
        FolderName.Check(raw).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("La Marca del Este", "La Marca del Este")]
    [InlineData("  Campañas  ", "Campañas")]
    [InlineData("Ñandú 2", "Ñandú 2")]
    [InlineData("a.b", "a.b")]
    [InlineData("Consejo", "Consejo")]
    [InlineData("COM10", "COM10")]
    public void Check_accepts_valid_names_and_trims_them(string raw, string expected) =>
        FolderName.Check(raw).Name.Should().Be(expected);

    [Fact]
    public void Check_gives_a_spanish_error_for_an_invalid_name() =>
        FolderName.Check("a/b").Error.Should().NotBeNullOrWhiteSpace();
}
