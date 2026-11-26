using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("0.3.0", "0.2.9", 1)]
    [InlineData("0.2.0", "0.2.0", 0)]
    [InlineData("0.2.0", "0.10.0", -1)]
    [InlineData("1.0.0", "1.0.0-beta.1", 1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    public void Compare_orders_semantic_versions(string a, string b, int expected) =>
        Math.Sign(ReleaseVersion.Compare(a, b)!.Value).Should().Be(expected);

    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData(" V1.4.2 ", "1.4.2")]
    [InlineData("1.4.2", "1.4.2")]
    public void Normalize_drops_the_leading_v(string tag, string expected) =>
        ReleaseVersion.Normalize(tag).Should().Be(expected);

    [Theory]
    [InlineData("latest", "0.2.0")]
    [InlineData("0.2", "0.2.0")]
    [InlineData("", "0.2.0")]
    [InlineData("99999999999999999999.0.0", "0.2.0")]
    [InlineData("0.2.0", "1.0.99999999999999999999")]
    [InlineData("١.٠.٠", "0.2.0")]
    public void Compare_returns_null_for_unreadable_versions(string a, string b) =>
        ReleaseVersion.Compare(a, b).Should().BeNull();
}
