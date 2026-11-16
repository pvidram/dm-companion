using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

internal sealed class CountingStorage(IVaultStorage inner, CountingFactory owner) : IVaultStorage
{
    public string RootPath => inner.RootPath;
    public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
    public async Task<VaultFileContent> ReadAsync(string p, CancellationToken ct = default)
    {
        if (MapPaths.IsStateFile(p))
        {
            Interlocked.Increment(ref owner.ReadsStarted);
            if (owner.ReadFailure is { } failure) throw failure;
            if (owner.ReadDelay > TimeSpan.Zero) await Task.Delay(owner.ReadDelay, ct);
        }
        return await inner.ReadAsync(p, ct);
    }
    public Task<byte[]> ReadBytesAsync(string p, CancellationToken ct = default) => inner.ReadBytesAsync(p, ct);
    public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> f, CancellationToken ct = default)
    {
        Interlocked.Increment(ref owner.FileListings);
        if (owner.ListFailure is { } failure) throw failure;
        return inner.ListFilesAsync(f, ct);
    }
    public Task<Stream> OpenReadAsync(string p, CancellationToken ct = default) => inner.OpenReadAsync(p, ct);
    public async Task<VaultFileInfo?> StatAsync(string p, CancellationToken ct = default)
    {
        var info = await inner.StatAsync(p, ct);
        if (MapPaths.IsStateFile(p)) Interlocked.Increment(ref owner.StateStats);
        if (MapPaths.IsStateFile(p) && info is not null && Interlocked.Exchange(ref owner.DeleteAfterNextStateStat, null) is { } path)
            File.Delete(path);
        return info;
    }
    public Task<WriteResult> WriteBytesAsync(string p, Stream c, WritePrecondition pre, CancellationToken ct = default) => inner.WriteBytesAsync(p, c, pre, ct);
    public Task<bool> MoveAsync(string a, string b, CancellationToken ct = default) => inner.MoveAsync(a, b, ct);
    public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
    public Task DeleteAsync(string p, CancellationToken ct = default) => inner.DeleteAsync(p, ct);

    public async Task<WriteResult> WriteAsync(string p, string c, WritePrecondition pre, CancellationToken ct = default)
    {
        if (!MapPaths.IsStateFile(p)) return await inner.WriteAsync(p, c, pre, ct);
        Interlocked.Increment(ref owner.WritesStarted);
        if (owner.FailWrites) throw new IOException("disco lleno (simulado)");
        if (owner.CrashWrites) throw new InvalidOperationException("fallo inesperado (simulado)");
        if (owner.WriteDelay > TimeSpan.Zero) await Task.Delay(owner.WriteDelay, ct);
        Interlocked.Exchange(ref owner.BeforeNextStateWrite, null)?.Invoke();
        lock (owner.StatePreconditions) owner.StatePreconditions.Add(pre.Mode);
        var result = await inner.WriteAsync(p, c, pre, ct);
        lock (owner.Writes) owner.Writes.Add(p);
        return result with { LastWriteTimeUtc = result.LastWriteTimeUtc + owner.WriteResultSkew };
    }
}
