using DmCompanion.Core.Glossary;
using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Índice vivo de las entradas del diario del vault abierto. Se reconstruye de forma perezosa cuando cambia el
/// <see cref="VaultIndex"/> (guardar, borrar o mover una nota lo cambia ya, sin esperar al watcher). Qué imágenes existen sale
/// de un listado de <c>Diario/</c> que se hace una vez por versión del índice y solo si hace falta (ver <see cref="Current"/>).
/// </summary>
public sealed class JournalIndex(VaultHost host, VaultFileUrls urls, ILogger<JournalIndex> logger) : IDisposable
{
    private readonly Lock _gate = new();
    private VaultService? _vault;
    private VaultIndex? _builtFor;
    private IReadOnlyList<JournalEntry> _entries = [];
    // Imágenes de Diario/ con su fecha (ruta normalizada, sin distinguir mayúsculas); null = sin listar.
    private Dictionary<string, DateTime>? _images;
    private VaultIndex? _relistedFor;
    private bool _listing;
    private int _generation;

    /// <summary>Cambió algo que el índice no avisa por <c>VaultHost.Changed</c>: el listado de imágenes. Puede llegar desde cualquier hilo.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<JournalEntry> Entries
    {
        get
        {
            lock (_gate) return Current();
        }
    }

    public JournalEntry? Get(string entryPath)
    {
        var path = VaultPaths.Normalize(entryPath);
        return Entries.FirstOrDefault(entry => entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<JournalEntry> ForPc(string pcNotePath)
    {
        var folder = JournalPaths.DiaryFolderOf(pcNotePath);
        return NewestFirst(Entries.Where(entry => entry.DiaryFolder.Equals(folder, StringComparison.OrdinalIgnoreCase)));
    }

    public string? OwnerPcPathOf(JournalEntry entry) =>
        host.Current?.Index.OfType(EntityType.Pc)
            .FirstOrDefault(pc => JournalPaths.DiaryFolderOf(pc.RelativePath).Equals(entry.DiaryFolder, StringComparison.OrdinalIgnoreCase))
            ?.RelativePath;

    /// <summary>
    /// Clave del glosario a la que apunta la entrada: <c>srd:…</c> tal cual, o <c>vault:ruta</c> si es un enlace (con o sin
    /// alias, ancla o carpeta) a una nota del vault. Null si no hay criatura o el enlace no resuelve.
    /// </summary>
    public string? CreatureKeyOf(JournalEntry entry)
    {
        if (entry.Creature is not { } raw) return null;
        if (raw.StartsWith(GlossaryPacks.SrdPrefix, StringComparison.Ordinal)) return raw;
        var target = raw.Trim().TrimStart('[').TrimEnd(']');
        target = target.Split('|')[0].Split('#')[0].Trim();
        return target.Length > 0 && host.Current?.Index.Resolve(target) is { } note ? GlossaryPacks.VaultKey(note.RelativePath) : null;
    }

    /// <summary>Nombre visible de la criatura enlazada de la entrada (el del glosario); null si no hay criatura o no resuelve.</summary>
    public string? CreatureNameOf(JournalEntry entry, GlossaryIndex glossary) =>
        CreatureKeyOf(entry) is { } key ? glossary.Get(key)?.Name : null;

    /// <summary>
    /// Entradas de esa criatura. Si una nota del vault tapa a la del SRD (o al revés), cuentan las que apuntan a cualquiera
    /// de las dos (spec §6).
    /// </summary>
    public IReadOnlyList<JournalEntry> ForCreature(string glossaryKey, GlossaryIndex glossary)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal) { glossaryKey };
        if (glossary.Get(glossaryKey)?.Overrides is { } covered) keys.Add(covered);
        if (glossary.OverriddenBy(glossaryKey) is { } cover) keys.Add(cover.Key);
        return NewestFirst(Entries.Where(entry => CreatureKeyOf(entry) is { } key && keys.Contains(key)));
    }

    public DateTime? LastWriteOf(string entryPath) => host.Current?.Index.Get(entryPath)?.LastWriteTimeUtc;

    /// <summary>URL virtual de la imagen (con su fecha para la caché del navegador); null si falta o el WebView no la muestra.</summary>
    public string? ImageUrl(JournalImage image)
    {
        if (!image.Exists || !image.Displayable) return null;
        DateTime stamp;
        lock (_gate) stamp = _images is not null && _images.TryGetValue(image.Path, out var known) ? known : default;
        return urls.UrlFor(image.Path, stamp);
    }

    /// <summary>Vuelve a listar las imágenes de <c>Diario/</c> ahora y avisa con <see cref="Changed"/>.</summary>
    public async Task RefreshImagesAsync(CancellationToken ct = default)
    {
        if (host.Current is not { } vault) return;
        int generation;
        lock (_gate)
        {
            if (!ReferenceEquals(vault, _vault)) Switch(vault);
            generation = _generation;
        }
        var images = await TryListAsync(vault, ct);
        lock (_gate)
        {
            if (generation != _generation || images is null) return;
            // Este listado sustituye al perezoso que pudiera estar en vuelo: sin soltar _listing, quien repinte ahora vería todo como existente.
            (_images, _relistedFor, _builtFor, _listing) = (images, vault.Index, null, false);
        }
        Raise();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_vault is not null) _vault.FileChanged -= OnFileChanged;
            _vault = null;
            _generation++;
        }
    }

    // Llamar con _gate tomado.
    private IReadOnlyList<JournalEntry> Current()
    {
        var vault = host.Current;
        if (!ReferenceEquals(vault, _vault)) Switch(vault);
        if (vault is null) return [];
        var index = vault.Index;
        if (ReferenceEquals(index, _builtFor)) return _entries;

        var images = _images;
        var optimistic = _listing || images is null;
        var entries = ReadEntries(index, optimistic ? _ => true : path => images!.ContainsKey(path));
        var missing = !optimistic && entries.Any(entry => entry.Images.Any(image => !image.Exists));
        if (!_listing && !ReferenceEquals(_relistedFor, index) && (images is null || missing))
        {
            // Una sola vez por versión del índice; mientras se lista, todo se da por existente.
            (_relistedFor, _listing) = (index, true);
            entries = ReadEntries(index, _ => true);
            (_entries, _builtFor) = (entries, index);
            // Va después de guardar la caché: si el listado acaba antes del primer await, RelistAsync entra en el lock (reentrante)
            // y suelta _builtFor aquí mismo; guardarlo después dejaría las entradas optimistas fijadas para siempre.
            _ = RelistAsync(vault, _generation);
            return entries;
        }
        (_entries, _builtFor) = (entries, index);
        return entries;
    }

    // Llamar con _gate tomado. Nuevo vault (o ninguno): se olvida todo y se vigilan sus imágenes.
    private void Switch(VaultService? vault)
    {
        if (_vault is not null) _vault.FileChanged -= OnFileChanged;
        _vault = vault;
        if (vault is not null) vault.FileChanged += OnFileChanged;
        _generation++;
        (_images, _relistedFor, _builtFor, _listing) = (null, null, null, false);
        _entries = [];
    }

    private async Task RelistAsync(VaultService vault, int generation)
    {
        var images = await TryListAsync(vault, CancellationToken.None);
        lock (_gate)
        {
            if (generation != _generation) return;
            _listing = false;
            if (images is not null) _images = images;
            _builtFor = null;
        }
        Raise();
    }

    private async Task<Dictionary<string, DateTime>?> TryListAsync(VaultService vault, CancellationToken ct)
    {
        try
        {
            var files = await vault.ListFilesAsync(JournalPaths.IsDiaryImage, ct);
            var images = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files) images[VaultPaths.Normalize(file.RelativePath)] = file.LastWriteTimeUtc;
            return images;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudieron listar las imágenes del diario ({ExceptionType}).", ex.GetType().Name);
            return null;
        }
    }

    // Solo una imagen de Diario/ (o un reescaneo) invalida el listado: con Syncthing llegan muchos cambios de otros ficheros.
    private void OnFileChanged(object? sender, VaultChange change)
    {
        if (change.Kind != VaultChangeKind.Rescan && !JournalPaths.IsDiaryImage(change.RelativePath)) return;
        lock (_gate) (_images, _relistedFor, _builtFor) = (null, null, null);
        Raise();
    }

    private static IReadOnlyList<JournalEntry> ReadEntries(VaultIndex index, Func<string, bool> exists) =>
        JournalLog.Order(index.Entities
            .Where(entity => JournalPaths.IsEntryPath(entity.RelativePath) && (entity.ParseError is not null || JournalReader.IsJournal(entity.Frontmatter)))
            .Select(entity => JournalReader.Read(entity.RelativePath, entity.Frontmatter, entity.Body, entity.ParseError, entity.LastWriteTimeUtc, exists)));

    private static IReadOnlyList<JournalEntry> NewestFirst(IEnumerable<JournalEntry> entries) =>
        [.. entries.OrderByDescending(entry => entry.Added.UtcDateTime).ThenByDescending(entry => entry.Path, StringComparer.Ordinal)];

    private void Raise() => SafeEvents.Raise(Changed, this, EventArgs.Empty, logger, "JournalIndex.Changed");
}
