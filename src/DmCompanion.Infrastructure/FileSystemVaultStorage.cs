using System.Text;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class FileSystemVaultStorage : IVaultStorage
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _rootWithSeparator;

    public FileSystemVaultStorage(string rootPath)
    {
        if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException($"No existe la carpeta del vault: {rootPath}");
        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        _rootWithSeparator = RootPath + Path.DirectorySeparatorChar;
    }

    public string RootPath { get; }

    public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<VaultFile>>(() => EnumerateRelativePaths()
            .Where(path => VaultPaths.IsIndexable(path) && IsSafe(path))
            .Select(path => new VaultFile(path, File.GetLastWriteTimeUtc(ToFullPath(path))))
            .ToList(), ct);

    public async Task<VaultFileContent> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ToFullPath(relativePath);
        // El timestamp se lee antes que el contenido: si alguien edita entremedias, un UnchangedSince posterior fallará (lado seguro).
        var lastWrite = File.GetLastWriteTimeUtc(fullPath);
        var content = await File.ReadAllTextAsync(fullPath, ct);
        return new VaultFileContent(VaultPaths.Normalize(relativePath), content, lastWrite);
    }

    public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> predicate, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<VaultFile>>(() => EnumerateRelativePaths()
            .Where(path => IsSafe(path) && predicate(path))
            .Select(path => new VaultFile(path, File.GetLastWriteTimeUtc(ToFullPath(path))))
            .ToList(), ct);

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new FileStream(ToFullPath(relativePath), new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        }));

    public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default) =>
        File.ReadAllBytesAsync(ToFullPath(relativePath), ct);

    public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default)
    {
        var info = new FileInfo(ToFullPath(relativePath));
        return Task.FromResult<VaultFileInfo?>(info.Exists ? new VaultFileInfo(info.Length, info.LastWriteTimeUtc) : null);
    }

    public Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default) =>
        WriteAtomicAsync(relativePath, precondition, tempPath => File.WriteAllTextAsync(tempPath, content, Utf8NoBom, ct));

    public Task<WriteResult> WriteBytesAsync(string relativePath, Stream content, WritePrecondition precondition, CancellationToken ct = default) =>
        WriteAtomicAsync(relativePath, precondition, async tempPath =>
        {
            await using var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(target, ct);
        });

    public Task<bool> MoveAsync(string fromRelativePath, string toRelativePath, CancellationToken ct = default)
    {
        var from = ToFullPath(fromRelativePath);
        var to = ToFullPath(toRelativePath);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(from != to && MoveChangingCase(from, to));
        if (File.Exists(to)) return Task.FromResult(false);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        try
        {
            File.Move(from, to, overwrite: false);
            return Task.FromResult(true);
        }
        catch (IOException) when (File.Exists(to))
        {
            return Task.FromResult(false);
        }
    }

    // En un sistema que no distingue mayúsculas (Android /sdcard) origen y destino son el mismo fichero: se pasa por un nombre temporal.
    // Si ya hay una entrada con el nombre exacto de destino, es otro fichero (sistema que sí las distingue) y no se toca.
    private static bool MoveChangingCase(string from, string to)
    {
        var directory = Path.GetDirectoryName(to)!;
        var targetName = Path.GetFileName(to);
        if (Directory.EnumerateFileSystemEntries(directory).Any(entry => Path.GetFileName(entry) == targetName)) return false;

        var temp = Path.Combine(directory, $".{targetName}.{Guid.NewGuid():N}.dmc.tmp");
        File.Move(from, temp, overwrite: false);
        try
        {
            File.Move(temp, to, overwrite: false);
        }
        catch
        {
            File.Move(temp, from, overwrite: false);
            throw;
        }
        return true;
    }

    // Escritura atómica: Syncthing nunca ve un fichero a medio escribir. Nombre único para que escrituras concurrentes no colisionen.
    private async Task<WriteResult> WriteAtomicAsync(string relativePath, WritePrecondition precondition, Func<string, Task> writeTemp)
    {
        var fullPath = ToFullPath(relativePath);
        var early = CheckPrecondition(fullPath, precondition);
        if (early is not null) return early;

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.dmc.tmp");
        try
        {
            await writeTemp(tempPath);

            // Reevaluar justo antes de mover para estrechar la ventana de carrera.
            var late = CheckPrecondition(fullPath, precondition);
            if (late is not null)
            {
                File.Delete(tempPath);
                return late;
            }

            try
            {
                File.Move(tempPath, fullPath, overwrite: precondition.Mode != WriteMode.MustNotExist);
            }
            catch (IOException) when (precondition.Mode == WriteMode.MustNotExist && File.Exists(fullPath))
            {
                File.Delete(tempPath);
                return new WriteResult(WriteStatus.AlreadyExists, File.GetLastWriteTimeUtc(fullPath));
            }
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
        return new WriteResult(WriteStatus.Written, File.GetLastWriteTimeUtc(fullPath));
    }

    private static WriteResult? CheckPrecondition(string fullPath, WritePrecondition precondition)
    {
        var exists = File.Exists(fullPath);
        var current = exists ? File.GetLastWriteTimeUtc(fullPath) : default;
        return precondition.Mode switch
        {
            WriteMode.MustNotExist when exists => new WriteResult(WriteStatus.AlreadyExists, current),
            // Un fichero borrado o inexistente cuenta como modificado: no se recrea en silencio.
            WriteMode.UnchangedSince when !exists || current != precondition.ExpectedLastWriteUtc =>
                new WriteResult(WriteStatus.ModifiedOnDisk, current),
            _ => null,
        };
    }

    // Ordenados por ruta: el orden de Directory.EnumerateFiles depende del sistema de ficheros y cambiaría de un equipo a otro.
    public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<SyncConflict>>(() => EnumerateRelativePaths()
            .Where(path => (VaultPaths.IsMarkdown(path) || MapPaths.IsStateFile(VaultPaths.GetConflictOriginal(path)))
                && !VaultPaths.IsExcluded(path) && VaultPaths.IsConflictFile(path) && IsSafe(path))
            .Order(StringComparer.Ordinal)
            .Select(path => new SyncConflict(VaultPaths.GetConflictOriginal(path), path))
            .ToList(), ct);

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        File.Delete(ToFullPath(relativePath));
        return Task.CompletedTask;
    }

    private IEnumerable<string> EnumerateRelativePaths() =>
        Directory.EnumerateFiles(RootPath, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint })
            .Select(fullPath => VaultPaths.Normalize(Path.GetRelativePath(RootPath, fullPath)));

    // Toda operación pasa por aquí: ruta segura (sin «..», absolutas, «:» ni segmentos ocultos) y sin enlaces que salgan de la raíz.
    private string ToFullPath(string relativePath)
    {
        if (!VaultPaths.TryNormalizeSafe(relativePath, out var normalized) || !TryResolveConfined(normalized, out var fullPath))
            throw new UnauthorizedAccessException($"La ruta «{relativePath}» no es válida o queda fuera del vault.");
        return fullPath;
    }

    private bool TryResolveConfined(string normalizedPath, out string fullPath)
    {
        fullPath = Path.GetFullPath(Path.Combine(RootPath, normalizedPath));
        if (!fullPath.StartsWith(_rootWithSeparator, PathComparison)) return false;

        // Se rechaza cualquier enlace simbólico en cualquier tramo (carpetas intermedias y fichero final): resolver
        // destinos no canoniza las carpetas intermedias y permite cadenas de enlaces que escapan del vault.
        var current = RootPath;
        foreach (var segment in normalizedPath.Split('/'))
        {
            current = Path.Combine(current, segment);
            var info = new FileInfo(current);
            // Primero el enlace (también si está colgante); un tramo que no existe no puede ser un enlace ni tener nada debajo.
            if (info.LinkTarget is not null) return false;
            if (!info.Exists && !Directory.Exists(current)) break;
        }
        return true;
    }

    private bool IsSafe(string path) => VaultPaths.TryNormalizeSafe(path, out var normalized) && TryResolveConfined(normalized, out _);
}
