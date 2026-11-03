using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure;

public sealed class DirectoryBrowser : IFolderBrowser
{
    public IReadOnlyList<string> ListSubdirectories(string path)
    {
        if (!Directory.Exists(path)) return [];
        return Directory.EnumerateDirectories(path, "*", new EnumerationOptions { IgnoreInaccessible = true })
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => !name.StartsWith('.'))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
