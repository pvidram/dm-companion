namespace DmCompanion.Core.Vault.Storage;

public sealed record VaultFile(string RelativePath, DateTime LastWriteTimeUtc);

public sealed record VaultFileInfo(long Length, DateTime LastWriteTimeUtc);

public sealed record VaultFileContent(string RelativePath, string Content, DateTime LastWriteTimeUtc);

public enum WriteMode { MustNotExist, UnchangedSince, Overwrite }

public sealed record WritePrecondition(WriteMode Mode, DateTime? ExpectedLastWriteUtc = null)
{
    public static WritePrecondition MustNotExist { get; } = new(WriteMode.MustNotExist);
    public static WritePrecondition Overwrite { get; } = new(WriteMode.Overwrite);
    public static WritePrecondition UnchangedSince(DateTime lastWriteTimeUtc) => new(WriteMode.UnchangedSince, lastWriteTimeUtc);
}

public enum WriteStatus { Written, AlreadyExists, ModifiedOnDisk }

public sealed record WriteResult(WriteStatus Status, DateTime LastWriteTimeUtc);

public sealed record SyncConflict(string OriginalRelativePath, string ConflictRelativePath);

public interface IVaultStorage
{
    string RootPath { get; }
    Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default);
    Task<VaultFileContent> ReadAsync(string relativePath, CancellationToken ct = default);
    Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default);
    // null si el fichero no existe; ruta fuera del vault → ArgumentException.
    Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default);
    Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default);
    Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default);
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}
