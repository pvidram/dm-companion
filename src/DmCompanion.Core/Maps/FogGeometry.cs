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

    /// <summary>El borde cuenta como dentro.</summary>
    public static bool Contains(IReadOnlyList<double> points, double x, double y)
    {
        var n = points.Count / 2;
        if (n < 3) return false;
        var inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = points[2 * i], yi = points[2 * i + 1], xj = points[2 * j], yj = points[2 * j + 1];
            if (OnSegment(xi, yi, xj, yj, x, y)) return true;
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    /// <summary>La última zona de la lista que contiene el punto (la creada más tarde).</summary>
    public static MapFogZone? ZoneAt(IReadOnlyList<MapFogZone> zones, double x, double y)
    {
        for (var i = zones.Count - 1; i >= 0; i--)
            if (Contains(zones[i].Points, x, y)) return zones[i];
        return null;
    }

    public static bool IsUnderFog(IReadOnlyList<MapFogZone> zones, double x, double y) =>
        zones.Any(z => !z.IsRevealed && Contains(z.Points, x, y));

    private static bool OnSegment(double ax, double ay, double bx, double by, double px, double py)
    {
        double dx = bx - ax, dy = by - ay;
        var lengthSq = dx * dx + dy * dy;
        var t = lengthSq < Epsilon ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lengthSq, 0, 1);
        double cx = ax + t * dx - px, cy = ay + t * dy - py;
        return Math.Sqrt(cx * cx + cy * cy) <= Epsilon;
    }
}
