namespace DmCompanion.Core.Maps;

public static class FogGeometry
{
    private const double Epsilon = 1e-9;
    private const double MinArea = 1e-6;

    /// <summary>Al menos 3 esquinas, valores finitos y área no nula.</summary>
    public static bool IsValid(IReadOnlyList<double> points)
    {
        if (points.Count < 6 || points.Count % 2 != 0 || points.Any(v => !double.IsFinite(v))) return false;
        var area = 0.0;
        for (int i = 0, n = points.Count / 2; i < n; i++)
        {
            var j = (i + 1) % n;
            area += points[2 * i] * points[2 * j + 1] - points[2 * j] * points[2 * i + 1];
        }
        return Math.Abs(area / 2) > MinArea;
    }

    /// <summary>Regla de vueltas (nonzero), la misma con la que Konva rellena la zona: un trazo que rodea un claro dos veces lo contiene. El borde cuenta como dentro.</summary>
    public static bool Contains(IReadOnlyList<double> points, double x, double y)
    {
        var n = points.Count / 2;
        if (n < 3) return false;
        var winding = 0;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = points[2 * i], yi = points[2 * i + 1], xj = points[2 * j], yj = points[2 * j + 1];
            if (OnSegment(xi, yi, xj, yj, x, y)) return true;
            var side = (xi - xj) * (y - yj) - (x - xj) * (yi - yj);
            if (yj <= y && yi > y && side > 0) winding++;
            else if (yj > y && yi <= y && side < 0) winding--;
        }
        return winding != 0;
    }

    /// <summary>La última zona de la lista que contiene el punto (la creada más tarde).</summary>
    public static MapFogZone? ZoneAt(IReadOnlyList<MapFogZone> zones, double x, double y)
    {
        for (var i = zones.Count - 1; i >= 0; i--)
            if (Contains(zones[i].Points, x, y)) return zones[i];
        return null;
    }

    /// <summary>Cualquier parte del círculo (borde incluido) que toque el polígono: centro dentro o alguna arista a distancia ≤ radio.</summary>
    public static bool IntersectsCircle(IReadOnlyList<double> points, double cx, double cy, double radius)
    {
        var n = points.Count / 2;
        if (n < 3) return false;
        if (Contains(points, cx, cy)) return true;
        for (int i = 0, j = n - 1; i < n; j = i++)
            if (DistanceToSegment(points[2 * i], points[2 * i + 1], points[2 * j], points[2 * j + 1], cx, cy) <= radius + Epsilon) return true;
        return false;
    }

    /// <summary>Un PJ solo se oculta por niebla total y por su centro; el resto, por cualquier zona tapada que toque su círculo (Size × cellSize / 2).</summary>
    public static bool IsHiddenByFog(MapToken token, IReadOnlyList<MapFogZone> zones, double cellSize)
    {
        var radius = token.Size * cellSize / 2;
        foreach (var zone in zones)
        {
            if (zone.IsRevealed) continue;
            if (token.Side == TokenSide.Pc)
            {
                if (!zone.IsNpc && Contains(zone.Points, token.X, token.Y)) return true;
            }
            else if (IntersectsCircle(zone.Points, token.X, token.Y, radius)) return true;
        }
        return false;
    }

    private static bool OnSegment(double ax, double ay, double bx, double by, double px, double py) =>
        DistanceToSegment(ax, ay, bx, by, px, py) <= Epsilon;

    private static double DistanceToSegment(double ax, double ay, double bx, double by, double px, double py)
    {
        double dx = bx - ax, dy = by - ay;
        var lengthSq = dx * dx + dy * dy;
        var t = lengthSq < Epsilon ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lengthSq, 0, 1);
        double cx = ax + t * dx - px, cy = ay + t * dy - py;
        return Math.Sqrt(cx * cx + cy * cy);
    }
}
