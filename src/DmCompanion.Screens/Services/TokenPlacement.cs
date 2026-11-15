using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

/// <summary>Dónde poner un token nuevo y dónde pintar su tarjeta rápida.</summary>
public static class TokenPlacement
{
    /// <summary>Lado de una casilla sin cuadrícula: el paso de la búsqueda y la medida de un token de tamaño 1.</summary>
    public const double FreeStep = 70;

    private const int MaxRings = 40;
    private const double Epsilon = 1e-6;

    // Tarjeta rápida: ancho máximo de `.ds-quick-card`, alto razonable y holguras.
    private const double CardWidth = 340;
    private const double CardHeight = 320;
    public const double CardMargin = 16;
    private const double CardGap = 32;

    /// <summary>
    /// Primer hueco libre alrededor del centro dado, buscando en anillos de casillas (o de 70 px sin cuadrícula)
    /// por distancia creciente. Si no queda ninguno en 40 anillos devuelve el centro ajustado, aunque se solape.
    /// </summary>
    public static (double X, double Y) FreeSpotNear(MapState state, GridSettings grid, int size, double centerX, double centerY)
    {
        var cell = grid.Enabled ? grid.CellSize : FreeStep;
        var (baseX, baseY) = GridMath.SnapCenter(grid, size, centerX, centerY);
        for (var ring = 0; ring <= MaxRings; ring++)
        {
            foreach (var (dx, dy) in Ring(ring))
            {
                var x = baseX + dx * cell;
                var y = baseY + dy * cell;
                if (!state.Tokens.Any(t => Overlaps(x, y, size, t, cell))) return (x, y);
            }
        }
        return (baseX, baseY);
    }

    /// <summary>
    /// Esquina superior izquierda de la tarjeta para un token en (tokenX, tokenY), px relativos al área del mapa:
    /// a la derecha del token, o a su izquierda si no cabe, y siempre dentro del área.
    /// </summary>
    public static (double X, double Y) CardPositionNear(double tokenX, double tokenY, double areaWidth, double areaHeight)
    {
        var x = tokenX + CardGap;
        if (x + CardWidth > areaWidth - CardMargin) x = tokenX - CardGap - CardWidth;
        return (Clamp(x, areaWidth - CardWidth), Clamp(tokenY - CardHeight / 2, areaHeight - CardHeight));
    }

    // Si el área es más pequeña que la tarjeta gana el margen izquierdo/superior.
    private static double Clamp(double value, double room) => Math.Max(CardMargin, Math.Min(value, room - CardMargin));

    private static bool Overlaps(double x, double y, int size, MapToken other, double cell)
    {
        var reach = (size + other.Size) * cell / 2;
        return Math.Abs(x - other.X) < reach - Epsilon && Math.Abs(y - other.Y) < reach - Epsilon;
    }

    // Casillas del anillo (distancia de Chebyshev = ring), de la más cercana al centro a la más lejana.
    private static IEnumerable<(int Dx, int Dy)> Ring(int ring)
    {
        if (ring == 0) return [(0, 0)];
        var cells = new List<(int Dx, int Dy)>();
        for (var dx = -ring; dx <= ring; dx++)
            for (var dy = -ring; dy <= ring; dy++)
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == ring) cells.Add((dx, dy));
        return cells.OrderBy(c => c.Dx * c.Dx + c.Dy * c.Dy).ThenBy(c => c.Dy).ThenBy(c => c.Dx);
    }
}
