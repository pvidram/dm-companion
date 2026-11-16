using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Maps;

public class FogGeometryTests
{
    private static readonly double[] Square = [0, 0, 10, 0, 10, 10, 0, 10];
    private static readonly double[] LShape = [0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10];

    [Fact]
    public void IsValid_accepts_a_triangle()
    {
        FogGeometry.IsValid([0, 0, 10, 0, 10, 10]).Should().BeTrue();
    }

    [Theory]
    [InlineData(new double[] { 0, 0, 10, 0 })]
    [InlineData(new double[] { 0, 0, 5, 5, 10, 10 })]
    [InlineData(new double[] { 0, 0, 10, 0, 10, 10, 0 })]
    [InlineData(new double[] { 0, 0, 10, double.NaN, 10, 10 })]
    [InlineData(new double[] { 0, 0, 10, 0, double.PositiveInfinity, 10 })]
    public void IsValid_rejects_degenerate_or_non_finite_polygons(double[] points)
    {
        FogGeometry.IsValid(points).Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(15, 5, false)]
    [InlineData(10, 5, true)]
    [InlineData(0, 0, true)]
    public void Contains_treats_the_border_as_inside(double x, double y, bool expected)
    {
        FogGeometry.Contains(Square, x, y).Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 8, true)]
    [InlineData(8, 8, false)]
    public void Contains_handles_concave_polygons(double x, double y, bool expected)
    {
        FogGeometry.Contains(LShape, x, y).Should().Be(expected);
    }

    [Fact]
    public void ZoneAt_returns_the_last_zone_that_contains_the_point()
    {
        var first = new MapFogZone("a", Square);
        var second = new MapFogZone("b", [2, 2, 8, 2, 8, 8, 2, 8]);

        FogGeometry.ZoneAt([first, second], 5, 5).Should().BeSameAs(second);
    }

    [Fact]
    public void IsUnderFog_is_false_for_a_revealed_zone()
    {
        FogGeometry.IsUnderFog([new MapFogZone("a", Square, true)], 5, 5).Should().BeFalse();
    }

    [Fact]
    public void IsUnderFog_is_true_for_a_covered_zone()
    {
        FogGeometry.IsUnderFog([new MapFogZone("a", Square)], 5, 5).Should().BeTrue();
    }
}
