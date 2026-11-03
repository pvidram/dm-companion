using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Infrastructure;

public sealed class FileSystemVaultServiceFactory(ILoggerFactory loggerFactory) : IVaultServiceFactory
{
    public VaultService Create(string rootPath) => new(
        new FileSystemVaultStorage(rootPath),
        new FileSystemVaultWatcher(rootPath),
        loggerFactory.CreateLogger<VaultService>());
}
