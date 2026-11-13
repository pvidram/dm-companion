namespace DmCompanion.Core.Maps;

public static class GridMath
{
    /// <summary>
    /// Ajusta el centro de un token a la cuadrícula: la esquina superior izquierda se alinea a una línea
    /// y el centro se deriva de ella según el tamaño. Con la cuadrícula desactivada devuelve la entrada.
    /// </summary>
    public static (double X, double Y) SnapCenter(GridSettings grid, int size, double x, double y)
    {
        if (!grid.Enabled) return (x, y);
        var half = size * grid.CellSize / 2;
        return (Snap(x, half, grid.CellSize, grid.OffsetX), Snap(y, half, grid.CellSize, grid.OffsetY));
    }

    private static double Snap(double v, double half, double cell, double offset) =>
        Math.Round((v - half - offset) / cell, MidpointRounding.AwayFromZero) * cell + offset + half;

    /// <summary>Distancia en pies (5 por casilla, diagonales como un paso). -1 si no hay cuadrícula.</summary>
    public static int DistanceFeet(GridSettings grid, double x1, double y1, double x2, double y2)
    {
        if (!grid.Enabled) return -1;
        var dx = Math.Abs(Cell(x2, grid.OffsetX, grid.CellSize) - Cell(x1, grid.OffsetX, grid.CellSize));
        var dy = Math.Abs(Cell(y2, grid.OffsetY, grid.CellSize) - Cell(y1, grid.OffsetY, grid.CellSize));
        return (int)Math.Max(dx, dy) * 5;
    }

    private static double Cell(double v, double offset, double cell) => Math.Floor((v - offset) / cell);
}
