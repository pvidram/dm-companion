namespace DmCompanion.Core.Maps;

public static class MapPaths
{
    public const string MapsFolder = "Mapas";

    private const string StateSuffix = ".state.json";
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];

    /// <summary>"Mapas/X.md" pasa a "Mapas/X.state.json", junto a la nota y con el mismo nombre base.</summary>
    public static string StatePathFor(string notePath)
    {
        var path = Vault.VaultPaths.Normalize(notePath);
        return (Vault.VaultPaths.IsMarkdown(path) ? path[..^3] : path) + StateSuffix;
    }

    public static bool IsImage(string path) =>
        ImageExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    public static bool IsStateFile(string path) =>
        path.EndsWith(StateSuffix, StringComparison.OrdinalIgnoreCase);
}
