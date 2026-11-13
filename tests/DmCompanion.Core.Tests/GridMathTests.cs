using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class GridMathTests
{
    private static readonly GridSettings Grid = new(true, 70, 0, 0);

    [Fact]
    public void SnapCenter_size_1_centers_in_the_cell()
    {
        var (x, y) = GridMath.SnapCenter(Grid, 1, 100, 100);
        x.Should().Be(105);
        y.Should().Be(105);
    }

    [Fact]
    public void SnapCenter_size_2_snaps_the_top_left_corner_to_the_grid()
    {
        var (x, y) = GridMath.SnapCenter(Grid, 2, 100, 100);
        x.Should().Be(70);
        y.Should().Be(70);
    }

    [Fact]
    public void SnapCenter_applies_the_offset()
    {
        var (x, y) = GridMath.SnapCenter(Grid with { OffsetX = 10 }, 1, 100, 100);
        x.Should().Be(115);
        y.Should().Be(105);
    }

    [Fact]
    public void SnapCenter_returns_the_input_when_the_grid_is_disabled()
    {
        var (x, y) = GridMath.SnapCenter(Grid with { Enabled = false }, 1, 101.5, 7);
        x.Should().Be(101.5);
        y.Should().Be(7);
    }

    [Fact]
    public void DistanceFeet_counts_diagonals_as_one_step() =>
        GridMath.DistanceFeet(Grid, 35, 35, 245, 105).Should().Be(15);

    [Fact]
    public void DistanceFeet_returns_minus_one_when_the_grid_is_disabled() =>
        GridMath.DistanceFeet(Grid with { Enabled = false }, 35, 35, 245, 105).Should().Be(-1);
}
