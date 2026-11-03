namespace DmCompanion.Core.Vault.Storage;

public interface IFolderBrowser
{
    IReadOnlyList<string> ListSubdirectories(string path);
}
