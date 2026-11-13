using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class HpEntryTests
{
    [Theory]
    [InlineData("9", 9)]
    [InlineData("0", 0)]
    [InlineData("  8  ", 8)]
    [InlineData("-5", 2)]
    [InlineData("\u22125", 2)]
    [InlineData("+3", 10)]
    public void Apply_accepts_absolute_and_relative_entries(string text, int expected)
    {
        var result = HpEntry.Apply(current: 7, max: 11, text);

        (result.Ok, result.Value).Should().Be((true, expected));
    }

    [Theory]
    [InlineData("99", 11)]
    [InlineData("+20", 11)]
    [InlineData("-20", 0)]
    [InlineData("-7", 0)]
    public void Apply_clamps_between_zero_and_the_maximum(string text, int expected) =>
        HpEntry.Apply(7, 11, text).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("+")]
    [InlineData("--3")]
    [InlineData("- 5")]
    [InlineData("3x")]
    [InlineData("+99999999999")]
    [InlineData("-99999999999")]
    [InlineData("99999999999")]
    public void Apply_rejects_text_that_is_not_an_integer(string? text) =>
        HpEntry.Apply(7, 11, text).Ok.Should().BeFalse();

    [Fact]
    public void Apply_does_not_overflow_when_adding_to_a_huge_current() =>
        HpEntry.Apply(int.MaxValue, int.MaxValue, "+5").Value.Should().Be(int.MaxValue);
}
