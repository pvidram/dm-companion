using DmCompanion.Core.Manuals;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Lee y escribe <c>&lt;manual&gt;.marcadores.json</c>. Escribe con precondición (MustNotExist / UnchangedSince),
/// fusiona y reintenta una vez ante un cambio externo, y no toca nunca un fichero corrupto.
/// </summary>
public sealed class ManualBookmarkStore(VaultHost host, ILogger<ManualBookmarkStore> logger)
{
    private readonly object _gate = new();
    private readonly HashSet<string> _corrupt = new(StringComparer.OrdinalIgnoreCase);
    // Vault al que pertenecen las rutas de _corrupt: al cambiar de vault la lista se vacía.
    private VaultService? _corruptVault;

    /// <summary>Ficheros de marcadores dañados del vault abierto (rutas relativas).</summary>
    public IReadOnlyList<string> CorruptFiles
    {
        get
        {
            lock (_gate)
            {
                ForgetOtherVault(host.Current);
                return [.. _corrupt.Order(StringComparer.OrdinalIgnoreCase)];
            }
        }
    }

    public async Task<BookmarksRead> LoadAsync(string pdfPath)
    {
        var (read, _) = await ReadAsync(host.Current, ManualPaths.BookmarksPathOf(pdfPath));
        return read;
    }

    public Task<(BookmarkSaveResult Result, ManualBookmarkFile? File)> AddAsync(string pdfPath, string name, int page) =>
        ChangeAsync(pdfPath, file => new ManualBookmarkFile(
            ManualBookmarks.CurrentVersion,
            [.. file.Bookmarks, new ManualBookmark(Guid.NewGuid().ToString("N"), name, page)]));

    public Task<(BookmarkSaveResult Result, ManualBookmarkFile? File)> RemoveAsync(string pdfPath, string bookmarkId) =>
        ChangeAsync(pdfPath, file => new ManualBookmarkFile(
            ManualBookmarks.CurrentVersion,
            [.. file.Bookmarks.Where(b => b.Id != bookmarkId)]));

    private async Task<(BookmarkSaveResult, ManualBookmarkFile?)> ChangeAsync(string pdfPath, Func<ManualBookmarkFile, ManualBookmarkFile> change)
    {
        var vault = host.Current;
        if (vault is null) return (BookmarkSaveResult.Failed, null);
        var path = ManualPaths.BookmarksPathOf(pdfPath);
        try
        {
            var (read, stamp) = await ReadAsync(vault, path);
            if (read.Corrupt) return (BookmarkSaveResult.Corrupt, null);

            var original = read.File ?? Empty;
            var mine = change(original);
            if (await TryWriteAsync(vault, path, mine, stamp)) return (BookmarkSaveResult.Saved, mine);

            // Cambio externo entre la lectura y la escritura: se fusiona sobre lo que hay ahora y se reintenta una vez.
            var (reread, restamp) = await ReadAsync(vault, path);
            if (reread.Corrupt) return (BookmarkSaveResult.Corrupt, null);
            var merged = ManualBookmarks.Merge(original, mine, reread.File ?? Empty);
            return await TryWriteAsync(vault, path, merged, restamp)
                ? (BookmarkSaveResult.Saved, merged)
                : (BookmarkSaveResult.Failed, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo guardar {Path} ({ExceptionType})", path, ex.GetType().Name);
            return (BookmarkSaveResult.Failed, null);
        }
    }

    private static ManualBookmarkFile Empty => new(ManualBookmarks.CurrentVersion, []);

    private async Task<(BookmarksRead Read, DateTime? Stamp)> ReadAsync(VaultService? vault, string path)
    {
        if (vault is null) return (new BookmarksRead(null, false), null);
        try
        {
            var info = await vault.StatAsync(path);
            if (info is null)
            {
                SetCorrupt(vault, path, false);
                return (new BookmarksRead(null, false), null);
            }
            var content = await vault.ReadRawAsync(path);
            var read = ManualBookmarks.Parse(content.Content);
            SetCorrupt(vault, path, read.Corrupt);
            if (read.Corrupt) logger.LogWarning("Marcadores dañados en {Path}", path);
            // La fecha que vale para la precondición es la de la lectura, no la del Stat previo.
            return (read, content.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo leer {Path} ({ExceptionType})", path, ex.GetType().Name);
            return (new BookmarksRead(null, false), null);
        }
    }

    private static async Task<bool> TryWriteAsync(VaultService vault, string path, ManualBookmarkFile file, DateTime? stamp)
    {
        var precondition = stamp is { } date ? WritePrecondition.UnchangedSince(date) : WritePrecondition.MustNotExist;
        var result = await vault.WriteAsync(path, ManualBookmarks.Serialize(file), precondition);
        return result.Status == WriteStatus.Written;
    }

    private void SetCorrupt(VaultService vault, string path, bool corrupt)
    {
        lock (_gate)
        {
            ForgetOtherVault(vault);
            if (corrupt) _corrupt.Add(path);
            else _corrupt.Remove(path);
        }
    }

    private void ForgetOtherVault(VaultService? vault)
    {
        if (ReferenceEquals(vault, _corruptVault)) return;
        _corrupt.Clear();
        _corruptVault = vault;
    }
}
