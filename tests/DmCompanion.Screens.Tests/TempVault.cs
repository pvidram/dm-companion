namespace DmCompanion.Screens.Tests;

internal sealed class TempVault : IDisposable
{
    private TempVault(string root) => Root = root;

    public string Root { get; }

    public static TempVault Empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "dmc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TempVault(root);
    }

    public static TempVault FromDemo()
    {
        var vault = Empty();
        var source = FindDemo();
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(vault.Root, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(vault.Root, Path.GetRelativePath(source, file)));
        return vault;
    }

    public string PathOf(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    private static string FindDemo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "vault-demo");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("No se encuentra samples/vault-demo subiendo desde el directorio de los tests.");
    }
}
