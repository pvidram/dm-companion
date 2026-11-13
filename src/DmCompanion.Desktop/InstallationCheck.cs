namespace DmCompanion.Desktop;

public static class InstallationCheck
{
    /// <summary>
    /// Comprueba que wwwroot está junto al ejecutable (zip descomprimido). En desarrollo (dotnet run) no existe esa
    /// carpeta, pero sí el manifiesto de activos estáticos, que también vale.
    /// </summary>
    public static bool IsComplete(string baseDirectory) =>
        Directory.Exists(Path.Combine(baseDirectory, "wwwroot"))
        || Directory.EnumerateFiles(baseDirectory, "*.staticwebassets.runtime.json").Any();
}
