namespace DmCompanion.Core.Vault.Storage;

public interface IVaultServiceFactory
{
    // Builds a not-yet-loaded VaultService for the given vault root. Throws DirectoryNotFoundException if it doesn't exist.
    VaultService Create(string rootPath);
}
