using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests.Services;

public sealed class ByteRangeTests
{
    private const long Total = 1000;

    [Fact]
    public void Closed_range()
    {
        ByteRanges.TryParse("bytes=10-19", Total, out var range).Should().Be(RangeParse.Ok);
        range.Should().Be(new ByteRange(10, 10));
    }

    [Fact]
    public void Open_ended_range()
    {
        ByteRanges.TryParse("bytes=990-", Total, out var range).Should().Be(RangeParse.Ok);
        range.Should().Be(new ByteRange(990, 10));
    }

    [Fact]
    public void Suffix_range()
    {
        ByteRanges.TryParse("bytes=-100", Total, out var range).Should().Be(RangeParse.Ok);
        range.Should().Be(new ByteRange(900, 100));
    }

    [Fact]
    public void Suffix_longer_than_file_is_whole_file()
    {
        ByteRanges.TryParse("bytes=-5000", Total, out var range).Should().Be(RangeParse.Ok);
        range.Should().Be(new ByteRange(0, Total));
    }

    [Fact]
    public void Start_beyond_length_is_unsatisfiable() =>
        ByteRanges.TryParse("bytes=1000-1100", Total, out _).Should().Be(RangeParse.Unsatisfiable);

    [Fact]
    public void End_beyond_length_is_clamped()
    {
        ByteRanges.TryParse("bytes=900-5000", Total, out var range).Should().Be(RangeParse.Ok);
        range.Should().Be(new ByteRange(900, 100));
    }

    [Fact]
    public void Multiple_ranges_are_ignored() =>
        ByteRanges.TryParse("bytes=0-9,20-29", Total, out _).Should().Be(RangeParse.None);

    [Theory]
    [InlineData("")]
    [InlineData("bytes=")]
    [InlineData("bytes=-")]
    [InlineData("bytes=abc-def")]
    [InlineData("items=0-9")]
    [InlineData("bytes=9-0")]
    [InlineData("bytes=-0")]
    public void Garbage_is_ignored(string header) =>
        ByteRanges.TryParse(header, Total, out _).Should().Be(RangeParse.None);

    [Fact]
    public void Null_is_none() =>
        ByteRanges.TryParse(null, Total, out _).Should().Be(RangeParse.None);

    [Fact]
    public void Empty_file_with_a_range_is_unsatisfiable() =>
        ByteRanges.TryParse("bytes=0-", 0, out _).Should().Be(RangeParse.Unsatisfiable);

    [Fact]
    public void Start_equal_to_total_is_unsatisfiable() =>
        ByteRanges.TryParse("bytes=1000-", Total, out _).Should().Be(RangeParse.Unsatisfiable);

    [Fact]
    public void Huge_number_is_ignored() =>
        ByteRanges.TryParse("bytes=0-99999999999999999999999", Total, out _).Should().Be(RangeParse.None);
}
