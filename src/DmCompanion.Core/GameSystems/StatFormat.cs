using System.Globalization;

namespace DmCompanion.Core.GameSystems;

public static class StatFormat
{
    public const string Dash = "—";

    public static string Signed(int v) =>
        v < 0 ? "−" + (-(long)v).ToString(CultureInfo.InvariantCulture) : "+" + v.ToString(CultureInfo.InvariantCulture);

    public static string Plain(int? v) =>
        v is null ? Dash : v < 0 ? Signed(v.Value) : v.Value.ToString(CultureInfo.InvariantCulture);
}
