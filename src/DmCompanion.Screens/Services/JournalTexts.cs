using System.Globalization;
using DmCompanion.Core.Journal;

namespace DmCompanion.Screens.Services;

/// <summary>Textos del diario que comparten varias pantallas (literales de la spec §8).</summary>
public static class JournalTexts
{
    /// <summary>La tarjeta que sustituye a una imagen que no se puede pintar; null si se puede.</summary>
    public static (string Title, string? Detail)? Placeholder(JournalImage image)
    {
        if (!image.Exists) return ($"Imagen no encontrada: {image.Name}", null);
        return image.Displayable ? null : ("Formato no compatible", image.Name);
    }

    public static string ImageCount(int count) => count == 1 ? "1 imagen" : $"{count.ToString(CultureInfo.InvariantCulture)} imágenes";

    public static string Date(JournalEntry entry) => entry.Added.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
