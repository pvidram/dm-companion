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
    public void Contains_includes_the_centre_of_a_path_that_loops_twice_around_it()
    {
        double[] twoLoops = [0, 0, 20, 0, 20, 20, 0, 20, 2, 2, 18, 2, 18, 18, 2, 18];

        FogGeometry.Contains(twoLoops, 10, 10).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(8, 5)]
    public void Contains_includes_both_lobes_of_an_asymmetric_figure_eight(double x, double y)
    {
        double[] figureEight = [0, 0, 10, 10, 10, 0, 0, 6];

        FogGeometry.Contains(figureEight, x, y).Should().BeTrue();
    }

    [Theory]
    [InlineData(5, 5, 1, true)]
    [InlineData(15, 5, 5, true)]
    [InlineData(15.1, 5, 5, false)]
    [InlineData(-3, -4, 5, true)]
    [InlineData(-3, -4, 4.9, false)]
    [InlineData(5, 5, 100, true)]
    public void IntersectsCircle_follows_the_circle_against_the_polygon(double x, double y, double r, bool expected) =>
        FogGeometry.IntersectsCircle(Square, x, y, r).Should().Be(expected);

    private static MapToken Tok(TokenSide side, double x, double y, int size = 1) =>
        new("t", "T", null, side, size, x, y, null, null);

    private static readonly double[] Big = [0, 0, 100, 0, 100, 100, 0, 100];
    private static MapFogZone Zone(FogType type, bool? revealed = null) => new("z", Big, revealed, type);

    [Theory]
    [InlineData(TokenSide.Enemy, FogType.Total, true)]
    [InlineData(TokenSide.Ally, FogType.Total, true)]
    [InlineData(TokenSide.Enemy, FogType.Npc, true)]
    [InlineData(TokenSide.Ally, FogType.Npc, true)]
    [InlineData(TokenSide.Pc, FogType.Total, true)]
    [InlineData(TokenSide.Pc, FogType.Npc, false)]
    public void IsHiddenByFog_for_a_token_at_the_zone_centre(TokenSide side, FogType type, bool expected) =>
        FogGeometry.IsHiddenByFog(Tok(side, 50, 50), [Zone(type)], 70).Should().Be(expected);

    [Theory]
    [InlineData(135, true)]
    [InlineData(135.5, false)]
    public void IsHiddenByFog_touches_for_an_npc_by_any_part_of_its_circle(double x, bool expected) =>
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Enemy, x, 50), [Zone(FogType.Npc)], 70).Should().Be(expected);

    [Fact] public void IsHiddenByFog_for_a_pc_next_to_total_fog_counts_only_the_centre() =>
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Pc, 120, 50), [Zone(FogType.Total)], 70).Should().BeFalse();

    [Fact] public void IsHiddenByFog_for_a_pc_on_the_border_of_total_fog_is_hidden() =>
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Pc, 100, 50), [Zone(FogType.Total)], 70).Should().BeTrue();

    [Fact] public void IsHiddenByFog_uses_the_token_size_for_the_radius() =>
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Enemy, 165, 50, size: 2), [Zone(FogType.Npc)], 70).Should().BeTrue();

    [Fact] public void IsHiddenByFog_uses_the_given_cell_size() =>
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Enemy, 145, 50), [Zone(FogType.Npc)], 100).Should().BeTrue();

    [Fact] public void IsHiddenByFog_ignores_revealed_zones() =>
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Enemy, 50, 50), [Zone(FogType.Npc, true)], 70).Should().BeFalse();

    [Fact] public void IsHiddenByFog_hides_when_any_of_overlapping_zones_of_different_type_hides()
    {
        var small = new MapFogZone("a", [0, 0, 10, 0, 10, 10, 0, 10], null, FogType.Total);
        FogGeometry.IsHiddenByFog(Tok(TokenSide.Enemy, 200, 200), [small, new MapFogZone("b", [180, 180, 190, 180, 190, 190, 180, 190], null, FogType.Npc)], 70)
            .Should().BeTrue();
    }
}
