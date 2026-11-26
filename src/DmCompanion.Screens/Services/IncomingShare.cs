using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

public sealed record SharedBatch(IReadOnlyList<SharedFile> Files, int Unreadable);

/// <summary>
/// Lo que llegó por «Compartir» y aún no está en el vault. La plataforma copia los flujos a <see cref="IShareInbox.TempFolder"/>
/// y los entrega aquí; el formulario «Nueva entrada» los toma. Nada se escribe en el vault hasta que el DM guarda.
/// Solo borra dentro de la carpeta temporal.
/// </summary>
public sealed class IncomingShare(ILogger<IncomingShare> logger, IShareInbox? inbox = null)
{
    private readonly Lock _gate = new();
    private readonly List<SharedFile> _files = [];
    private int _unreadable;

    /// <summary>Puede llegar desde cualquier hilo.</summary>
    public event EventHandler? Received;

    public bool HasPending
    {
        get
        {
            lock (_gate) return _files.Count > 0 || _unreadable > 0;
        }
    }

    public void Receive(IReadOnlyList<SharedFile> files, int unreadable)
    {
        if (files.Count == 0 && unreadable == 0) return;
        lock (_gate)
        {
            _files.AddRange(files);
            _unreadable += unreadable;
        }
        SafeEvents.Raise(Received, this, EventArgs.Empty, logger, nameof(Received));
    }

    public SharedBatch Take()
    {
        lock (_gate)
        {
            var batch = new SharedBatch([.. _files], _unreadable);
            _files.Clear();
            _unreadable = 0;
            return batch;
        }
    }

    public Stream Open(SharedFile file) =>
        new FileStream(file.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

    public void Discard(IEnumerable<SharedFile> files)
    {
        foreach (var file in files)
        {
            if (!IsInsideTemp(file.TempPath))
            {
                logger.LogWarning("Se ignora el borrado de un fichero fuera de la carpeta temporal del «Compartir».");
                continue;
            }
            try
            {
                File.Delete(file.TempPath);
                var folder = Path.GetDirectoryName(file.TempPath)!;
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("No se pudo borrar un temporal del «Compartir» ({ExceptionType})", ex.GetType().Name);
            }
        }
    }

    public void CleanTemp()
    {
        if (inbox is null || !Directory.Exists(inbox.TempFolder)) return;
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(inbox.TempFolder))
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else File.Delete(entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo vaciar la carpeta temporal del «Compartir» ({ExceptionType})", ex.GetType().Name);
        }
    }

    public static string? ExtensionForMime(string? mimeType) => mimeType?.Trim().ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "image/heic" or "image/heif" => ".heic",
        _ => null,
    };

    private bool IsInsideTemp(string path)
    {
        if (inbox is null) return false;
        var root = Path.GetFullPath(inbox.TempFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal);
    }
}
