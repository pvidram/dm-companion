using System.Text.RegularExpressions;

namespace DmCompanion.UI.Components;

public static partial class IconFiles
{
    public const string BasePath = "_content/DmCompanion.UI/icons/";

    // Guion antes de cada mayúscula y antes de cada grupo de cifras que sigue a una letra: Trash2 → trash-2, Grid3X3 → grid-3-x-3.
    [GeneratedRegex("(?<!^)([A-Z]|(?<=[A-Za-z])[0-9]+)")]
    private static partial Regex WordStart();

    public static string FileName(IconName name) =>
        WordStart().Replace(name.ToString(), "-$1").ToLowerInvariant() + ".svg";

    public static string Url(IconName name) => BasePath + FileName(name);
}
