using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure.Tests;

internal sealed class ManualChangeSource : IVaultChangeSource
{
    public event EventHandler<VaultChange>? Changed;

    public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;

    public bool Started { get; private set; }

    public void Start() => Started = true;

    public void Raise(string relativePath, VaultChangeKind kind) => Changed?.Invoke(this, new VaultChange(relativePath, kind));

    public void Dispose() { }
}
