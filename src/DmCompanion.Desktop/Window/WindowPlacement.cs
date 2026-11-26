namespace DmCompanion.Desktop.Window;

/// <summary>Decide dónde se abre la ventana a partir de lo guardado y de las pantallas disponibles.</summary>
public static class WindowPlacement
{
    public const int MinWidth = 1024;
    public const int MinHeight = 640;

    /// <summary>Píxeles de ancho y de alto que deben verse en alguna pantalla para restaurar la posición.</summary>
    public const int MinVisible = 100;

    /// <summary>Lo guardado, con el tamaño mínimo aplicado, o null para abrir maximizada.</summary>
    public static WindowBounds? Restore(WindowBounds? saved, IReadOnlyList<ScreenArea> screens)
    {
        if (saved is null) return null;
        var bounds = saved with { Width = Math.Max(saved.Width, MinWidth), Height = Math.Max(saved.Height, MinHeight) };
        return screens.Any(screen => IsVisibleOn(bounds, screen)) ? bounds : null;
    }

    private static bool IsVisibleOn(WindowBounds bounds, ScreenArea screen)
    {
        var visibleWidth = Math.Min(bounds.X + bounds.Width, screen.X + screen.Width) - Math.Max(bounds.X, screen.X);
        var visibleHeight = Math.Min(bounds.Y + bounds.Height, screen.Y + screen.Height) - Math.Max(bounds.Y, screen.Y);
        return visibleWidth >= MinVisible && visibleHeight >= MinVisible;
    }
}
