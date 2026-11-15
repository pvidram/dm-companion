namespace DmCompanion.Core.Tv;

/// <summary>Encuadre de la TV sobre el mapa, en píxeles del mapa.</summary>
public sealed record TvFrame(double X, double Y, double Width, double Height)
{
    /// <summary>Agranda el lado corto hasta la proporción pedida, manteniendo el centro y sin recortar. Aspecto no válido: sin cambios.</summary>
    public TvFrame FitToAspect(double aspect)
    {
        if (!double.IsFinite(aspect) || aspect <= 0 || Width <= 0 || Height <= 0) return this;
        var current = Width / Height;
        if (Math.Abs(current - aspect) < 1e-9) return this;
        var cx = X + Width / 2;
        var cy = Y + Height / 2;
        if (current < aspect)
        {
            var w = Height * aspect;
            return new TvFrame(cx - w / 2, Y, w, Height);
        }
        var h = Width / aspect;
        return new TvFrame(X, cy - h / 2, Width, h);
    }
}
