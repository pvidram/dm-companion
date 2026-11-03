namespace DmCompanion.UI.Tests.Support;

internal static class UiAssets
{
    public static string PathOf(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "ui-wwwroot", relativePath);

    public static string Read(string relativePath) => File.ReadAllText(PathOf(relativePath));
}
