namespace DmCompanion.Core.Vault.Storage;

public enum VaultChangeKind { Upserted, Deleted, Rescan }

public sealed record VaultChange(string RelativePath, VaultChangeKind Kind);

public interface IVaultChangeSource : IDisposable
{
    event EventHandler<VaultChange>? Changed;
    void Start();
}
