using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public class FadeTests
{
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1000, 0.5)]
    [InlineData(2000, 1.0)]
    [InlineData(5000, 1.0)]
    [InlineData(-100, 0.0)]
    public void VolumeAt_is_linear_and_clamped(int elapsedMs, double expected) =>
        Fade.VolumeAt(0, 1, TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromSeconds(2)).Should().BeApproximately(expected, 1e-9);

    [Fact]
    public void VolumeAt_goes_down_too() =>
        Fade.VolumeAt(0.8, 0.0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)).Should().BeApproximately(0.4, 1e-9);

    [Fact]
    public void VolumeAt_with_zero_duration_is_the_target() =>
        Fade.VolumeAt(0.2, 0.9, TimeSpan.Zero, TimeSpan.Zero).Should().Be(0.9);
}
