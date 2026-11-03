using System.Text;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class FileSystemVaultStorage : IVaultStorage
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
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
            .Where(VaultPaths.IsIndexable)
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

    public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default) =>
        File.ReadAllBytesAsync(ToFullPath(relativePath), ct);

    public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default)
    {
        var info = new FileInfo(ToFullPath(relativePath));
        return Task.FromResult<VaultFileInfo?>(info.Exists ? new VaultFileInfo(info.Length, info.LastWriteTimeUtc) : null);
    }

    public async Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default)
    {
        var fullPath = ToFullPath(relativePath);
        var early = CheckPrecondition(fullPath, precondition);
        if (early is not null) return early;

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        // Escritura atómica: Syncthing nunca ve un fichero a medio escribir. Nombre único para que escrituras concurrentes no colisionen.
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.dmc.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, content, Utf8NoBom, ct);

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

    public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<SyncConflict>>(() => EnumerateRelativePaths()
            .Where(path => VaultPaths.IsMarkdown(path) && !VaultPaths.IsExcluded(path) && VaultPaths.IsConflictFile(path))
            .Select(path => new SyncConflict(VaultPaths.GetConflictOriginal(path), path))
            .ToList(), ct);

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        File.Delete(ToFullPath(relativePath));
        return Task.CompletedTask;
    }

    private IEnumerable<string> EnumerateRelativePaths() =>
        Directory.EnumerateFiles(RootPath, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Select(fullPath => VaultPaths.Normalize(Path.GetRelativePath(RootPath, fullPath)));

    private string ToFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, VaultPaths.Normalize(relativePath)));
        if (!fullPath.StartsWith(_rootWithSeparator, StringComparison.Ordinal))
            throw new ArgumentException($"La ruta «{relativePath}» queda fuera del vault.", nameof(relativePath));
        return fullPath;
    }
}
