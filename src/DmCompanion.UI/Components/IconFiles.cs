using System.Text.RegularExpressions;

namespace DmCompanion.UI.Components;

public static partial class IconFiles
{
    public const string BasePath = "_content/DmCompanion.UI/icons/";

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex WordStart();

    public static string FileName(IconName name) =>
        WordStart().Replace(name.ToString(), "-$1").ToLowerInvariant() + ".svg";

    public static string Url(IconName name) => BasePath + FileName(name);
}
