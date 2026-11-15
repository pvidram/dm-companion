using DmCompanion.Core.Tv;

namespace DmCompanion.Core.Tests.Tv;

public class TvFrameTests
{
    [Fact]
    public void Frame_already_at_aspect_is_unchanged() =>
        new TvFrame(0, 0, 1600, 900).FitToAspect(16.0 / 9).Should().Be(new TvFrame(0, 0, 1600, 900));

    [Fact]
    public void Square_frame_widens_to_16_9_around_the_same_center()
    {
        var f = new TvFrame(0, 0, 1000, 1000).FitToAspect(16.0 / 9);
        f.Width.Should().BeApproximately(1000 * 16.0 / 9, 1e-6);
        f.Height.Should().Be(1000);
        (f.X + f.Width / 2).Should().BeApproximately(500, 1e-6);
        (f.Y + f.Height / 2).Should().BeApproximately(500, 1e-6);
    }

    [Fact]
    public void Wide_frame_grows_taller_for_4_3()
    {
        var f = new TvFrame(0, 0, 1600, 900).FitToAspect(4.0 / 3);
        (f.Width, f.Height, f.Y + f.Height / 2).Should().Be((1600.0, 1200.0, 450.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_aspect_leaves_frame_unchanged(double aspect) =>
        new TvFrame(5, 6, 1600, 900).FitToAspect(aspect).Should().Be(new TvFrame(5, 6, 1600, 900));
}
