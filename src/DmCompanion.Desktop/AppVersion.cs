using System.Reflection;

namespace DmCompanion.Desktop;

public static class AppVersion
{
    /// <summary>Versión de la app sin el sufijo «+commit» que añade el SDK.</summary>
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { Length: > 0 } raw
            ? raw.Split('+')[0]
            : "0.0.0";
}
