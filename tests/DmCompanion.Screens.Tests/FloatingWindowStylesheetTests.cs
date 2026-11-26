using System.Text.RegularExpressions;

namespace DmCompanion.Screens.Tests;

/// <summary>Un tamaño elegido no debe toparse con un max-height fijo: solo vale el límite de QuickCard (área del mapa menos margen).</summary>
public class FloatingWindowStylesheetTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src/DmCompanion.Screens/wwwroot/css/screens.css"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "src/DmCompanion.Screens/wwwroot/css/screens.css"));
    }

    [Theory]
    [InlineData(@"\.ds-quick-card\.combat-history-popover")]
    [InlineData(@"\.ds-quick-card\.sound-quick")]
    public void Floating_window_rules_set_no_max_height(string selector)
    {
        Regex.IsMatch(Css(), selector + @"\s*\{[^}]*max-height").Should().BeFalse();
    }
}
