namespace DmCompanion.Screens.Services;

/// <summary>Imagen nueva: su nombre original (da la extensión) y un flujo que se abre una sola vez.</summary>
public sealed record JournalImageSource(string FileName, Func<Task<Stream>> OpenAsync);

/// <summary>Una imagen de la entrada al editarla, en su orden: una que ya estaba (por su nombre) o una nueva.</summary>
public sealed record JournalImageItem(string? ExistingName, JournalImageSource? Added)
{
    public static JournalImageItem Keep(string name) => new(name, null);

    public static JournalImageItem Add(JournalImageSource source) => new(null, source);
}

public sealed record JournalCreate(string PcNotePath, string? Title, string? Note, string? Creature, IReadOnlyList<JournalImageSource> Images);

/// <param name="ExpectedLastWriteUtc">Fecha de la nota cuando se abrió el formulario (<c>JournalIndex.LastWriteOf</c>).</param>
public sealed record JournalEdit(string EntryPath, DateTime ExpectedLastWriteUtc, string? Title, string? Note, string? Creature,
    IReadOnlyList<JournalImageItem> Images);

public enum JournalStatus { Done, NoVault, NoImages, NotFound, ModifiedOnDisk, Failed }

/// <param name="EntryPath">Ruta de la nota guardada (la nueva, si se renombró). Null si no se guardó.</param>
/// <param name="Unreadable">Imágenes descartadas por no poder leerse o no ser de un tipo admitido.</param>
public sealed record JournalResult(JournalStatus Status, string? EntryPath = null, int Unreadable = 0)
{
    public bool IsDone => Status == JournalStatus.Done;
}
