namespace DmCompanion.Core.Maps;

public static class TokenNaming
{
    /// <summary>Devuelve el nombre base si está libre; si no, "base 2", "base 3"... sin distinguir mayúsculas.</summary>
    public static string Next(string baseName, IEnumerable<string> existingNames)
    {
        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName)) return baseName;
        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }
}
