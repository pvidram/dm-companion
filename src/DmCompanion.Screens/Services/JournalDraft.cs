using DmCompanion.Core.Journal;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Services;

public enum DraftImageKind { Existing, Picked, Shared }

/// <param name="Existing">La imagen ya guardada (solo con <see cref="DraftImageKind.Existing"/>).</param>
/// <param name="Open">Abre el flujo de una imagen elegida (solo con <see cref="DraftImageKind.Picked"/>).</param>
/// <param name="Shared">El temporal de una imagen compartida (solo con <see cref="DraftImageKind.Shared"/>).</param>
public sealed record DraftImage(string Id, string Name, DraftImageKind Kind, JournalImage? Existing = null,
    Func<Task<Stream>>? Open = null, SharedFile? Shared = null);

/// <summary>
/// Las imágenes de una entrada mientras se rellena el formulario: añadir, quitar y reordenar, sin tocar el vault. Nunca se queda
/// sin imágenes (decisión 4: para eso está «Borrar»).
/// </summary>
public sealed class JournalDraft
{
    private readonly List<DraftImage> _images = [];
    private readonly List<SharedFile> _droppedShared = [];
    private int _next;

    public IReadOnlyList<DraftImage> Images => _images;

    public int Count => _images.Count;

    public bool CanRemove => _images.Count > 1;

    /// <summary>Todos los temporales de «Compartir», también los de imágenes que se quitaron: al cerrar el formulario se borran.</summary>
    public IReadOnlyList<SharedFile> AllSharedFiles =>
        [.. _images.Where(image => image.Shared is not null).Select(image => image.Shared!), .. _droppedShared];

    public void AddExisting(JournalImage image) => _images.Add(new DraftImage(NewId(), image.Name, DraftImageKind.Existing, Existing: image));

    public void AddPicked(string fileName, Func<Task<Stream>> open) => _images.Add(new DraftImage(NewId(), fileName, DraftImageKind.Picked, Open: open));

    public void AddShared(SharedFile file) => _images.Add(new DraftImage(NewId(), file.FileName, DraftImageKind.Shared, Shared: file));

    public bool Remove(string id)
    {
        var index = IndexOf(id);
        if (index < 0 || !CanRemove) return false;
        if (_images[index].Shared is { } shared) _droppedShared.Add(shared);
        _images.RemoveAt(index);
        return true;
    }

    public bool MoveUp(string id) => Move(id, -1);

    public bool MoveDown(string id) => Move(id, +1);

    /// <summary>Para una entrada nueva: todas las imágenes son nuevas.</summary>
    public IReadOnlyList<JournalImageSource> ToSources(Func<SharedFile, Stream> openShared) =>
        [.. _images.Select(image => SourceOf(image, openShared))];

    /// <summary>Para editar: las guardadas se conservan por su nombre y las nuevas se añaden, todas en el orden del borrador.</summary>
    public IReadOnlyList<JournalImageItem> ToItems(Func<SharedFile, Stream> openShared) =>
        [.. _images.Select(image => image.Kind == DraftImageKind.Existing ? JournalImageItem.Keep(image.Name) : JournalImageItem.Add(SourceOf(image, openShared)))];

    private static JournalImageSource SourceOf(DraftImage image, Func<SharedFile, Stream> openShared) => image switch
    {
        { Shared: { } shared } => new JournalImageSource(shared.FileName, () => Task.FromResult(openShared(shared))),
        { Open: { } open } => new JournalImageSource(image.Name, open),
        _ => throw new InvalidOperationException("Una imagen ya guardada no es una imagen nueva."),
    };

    private bool Move(string id, int step)
    {
        var index = IndexOf(id);
        var target = index + step;
        if (index < 0 || target < 0 || target >= _images.Count) return false;
        (_images[index], _images[target]) = (_images[target], _images[index]);
        return true;
    }

    private int IndexOf(string id) => _images.FindIndex(image => image.Id == id);

    private string NewId() => $"img{++_next}";
}
