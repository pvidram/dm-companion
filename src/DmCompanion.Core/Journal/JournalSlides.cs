using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Journal;

/// <param name="ImagePath">Imagen de la diapositiva; null en la página de la nota.</param>
/// <param name="Text">La nota (solo en la última página de una entrada).</param>
public sealed record JournalSlide(string EntryPath, string Owner, string Title, int ImageNumber, int ImageCount,
    string? ImagePath, string? Text, int EntryNumber, int EntryCount)
{
    /// <summary>Pie de la TV (spec §7): «PJ · título · imagen i de N»; en la página de la nota, «PJ · título».</summary>
    public string Caption => ImagePath is null ? $"{Owner} · {Title}" : $"{Owner} · {Title} · imagen {ImageNumber} de {ImageCount}";

    /// <summary>Etiqueta de la barra de la tablet: «Entrada x de M» en la bitácora; «Imagen i de N» o «Nota» en una entrada.</summary>
    public string BarLabel(bool isLog) =>
        isLog ? $"Entrada {EntryNumber} de {EntryCount}" : ImagePath is null ? "Nota" : $"Imagen {ImageNumber} de {ImageCount}";
}

public static class JournalSlides
{
    /// <summary>Una diapositiva por imagen que se puede mostrar y, si la entrada tiene nota, una última con su texto.</summary>
    public static IReadOnlyList<JournalSlide> ForEntry(JournalEntry entry)
    {
        var shown = Shown(entry);
        var slides = shown
            .Select((image, index) => new JournalSlide(entry.Path, entry.OwnerName, entry.Title, index + 1, shown.Count, image.Path, null, 1, 1))
            .ToList();
        // La TV es para los jugadores: sin los bloques %%secreto%% y, en una entrada secreta, sin nota (las imágenes sí van).
        var note = entry.IsSecret ? string.Empty : SecretFilter.RemoveSecretBlocksForPlayers(entry.Note).Trim();
        if (note.Length > 0)
            slides.Add(new JournalSlide(entry.Path, entry.OwnerName, entry.Title, shown.Count, shown.Count, null, note, 1, 1));
        return slides;
    }

    /// <summary>Solo imágenes, en orden de bitácora. «Entrada x de M» cuenta las entradas que aportan alguna.</summary>
    public static IReadOnlyList<JournalSlide> ForLog(IEnumerable<JournalEntry> entries)
    {
        var withImages = JournalLog.Order(entries).Select(entry => (Entry: entry, Images: Shown(entry))).Where(x => x.Images.Count > 0).ToList();
        var slides = new List<JournalSlide>();
        for (var entryIndex = 0; entryIndex < withImages.Count; entryIndex++)
        {
            var (entry, images) = withImages[entryIndex];
            for (var imageIndex = 0; imageIndex < images.Count; imageIndex++)
                slides.Add(new JournalSlide(entry.Path, entry.OwnerName, entry.Title, imageIndex + 1, images.Count, images[imageIndex].Path, null,
                    entryIndex + 1, withImages.Count));
        }
        return slides;
    }

    // Una imagen que falta o que el WebView no muestra (HEIC) se salta, también en la TV.
    private static IReadOnlyList<JournalImage> Shown(JournalEntry entry) => [.. entry.Images.Where(image => image.Exists && image.Displayable)];
}
