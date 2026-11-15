namespace DmCompanion.Screens.Components.Sound;

/// <summary>Textos de los avisos de sonido. Llevan rutas y etiquetas visibles para el DM, nunca se registran.</summary>
internal static class SoundNotices
{
    public static string Missing(IReadOnlyList<string> paths) => "Falta: " + string.Join(", ", paths);

    public static string NotFound(string label) => "No se encuentra: " + label;
}
