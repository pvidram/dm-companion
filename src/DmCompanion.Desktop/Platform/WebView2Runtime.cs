using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DmCompanion.Desktop.Platform;

/// <summary>Comprobación documentada por Microsoft de que el runtime de WebView2 está instalado (valor <c>pv</c> del registro).</summary>
public static class WebView2Runtime
{
    private const string ClientKey = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private const string ClientKeyWow64 = @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    /// <summary>Las rutas se piden con el prefijo de la colmena (<c>HKLM\...</c> o <c>HKCU\...</c>) y <paramref name="readPv"/> devuelve el valor <c>pv</c> o null.</summary>
    public static bool IsInstalled(Func<string, string?> readPv) =>
        new[] { @"HKLM\" + ClientKeyWow64, @"HKLM\" + ClientKey, @"HKCU\" + ClientKey }
            .Select(readPv)
            .Any(pv => !string.IsNullOrWhiteSpace(pv) && pv != "0.0.0.0");

    [SupportedOSPlatform("windows")]
    public static bool IsInstalled() => IsInstalled(ReadFromRegistry);

    [SupportedOSPlatform("windows")]
    private static string? ReadFromRegistry(string path)
    {
        var split = path.IndexOf('\\');
        var hive = path[..split] == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        using var key = hive.OpenSubKey(path[(split + 1)..]);
        return key?.GetValue("pv") as string;
    }
}
