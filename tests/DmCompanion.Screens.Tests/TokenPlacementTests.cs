using DmCompanion.Core.Maps;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class TokenPlacementTests
{
    private static readonly GridSettings Grid = new(true, 70, 0, 0);

    private static MapState With(params MapToken[] tokens) => MapState.Empty with { Grid = Grid, Tokens = tokens };

    private static MapToken Token(double x, double y, int size = 1) =>
        new("t" + x + y, "T", null, TokenSide.Enemy, size, x, y, null, null);

    private static bool Overlap(double x1, double y1, int s1, MapToken other, double cell)
    {
        var reach = (s1 + other.Size) * cell / 2;
        return Math.Abs(x1 - other.X) < reach - 1e-6 && Math.Abs(y1 - other.Y) < reach - 1e-6;
    }

    [Fact]
    public void Free_center_cell_is_used_as_is()
    {
        TokenPlacement.FreeSpotNear(With(), Grid, 1, 35, 35).Should().Be((35.0, 35.0));
    }

    [Fact]
    public void Occupied_center_cell_returns_an_adjacent_free_cell()
    {
        var (x, y) = TokenPlacement.FreeSpotNear(With(Token(35, 35)), Grid, 1, 35, 35);

        (Math.Abs(x - 35), Math.Abs(y - 35)).Should().BeOneOf((70.0, 0.0), (0.0, 70.0), (70.0, 70.0));
    }

    [Fact]
    public void Large_token_does_not_overlap_a_small_one()
    {
        var existing = Token(35, 35);

        var (x, y) = TokenPlacement.FreeSpotNear(With(existing), Grid, 2, 35, 35);

        Overlap(x, y, 2, existing, 70).Should().BeFalse();
    }

    [Fact]
    public void Small_token_does_not_overlap_a_large_one()
    {
        var existing = Token(70, 70, size: 2);

        var (x, y) = TokenPlacement.FreeSpotNear(With(existing), Grid, 1, 70, 70);

        Overlap(x, y, 1, existing, 70).Should().BeFalse();
    }

    [Fact]
    public void Without_grid_it_steps_by_70_pixels()
    {
        var off = GridSettings.Default;
        var state = MapState.Empty with { Tokens = [Token(100, 100)] };

        var (x, y) = TokenPlacement.FreeSpotNear(state, off, 1, 100, 100);

        (Math.Abs(x - 100) >= 70 || Math.Abs(y - 100) >= 70).Should().BeTrue();
    }

    [Fact]
    public void Surrounded_center_finds_a_spot_in_the_next_ring()
    {
        var ring = from dx in new[] { -70, 0, 70 } from dy in new[] { -70, 0, 70 } select Token(35 + dx, 35 + dy);

        var (x, y) = TokenPlacement.FreeSpotNear(With([.. ring]), Grid, 1, 35, 35);

        ring.Any(t => Overlap(x, y, 1, t, 70)).Should().BeFalse();
    }

    [Theory]
    [InlineData(500, 400, 1000, 800, 532, 240)]   // cabe a la derecha del token
    [InlineData(900, 400, 1000, 800, 528, 240)]   // no cabe: pasa a la izquierda
    [InlineData(500, 10, 1000, 800, 532, 16)]     // arriba: se pega al borde superior
    [InlineData(500, 790, 1000, 800, 532, 464)]   // abajo: se pega al borde inferior
    public void Card_position_stays_inside_the_map_area(double tokenX, double tokenY, double areaW, double areaH, double expectedX, double expectedY)
    {
        TokenPlacement.CardPositionNear(tokenX, tokenY, areaW, areaH).Should().Be((expectedX, expectedY));
    }
}
