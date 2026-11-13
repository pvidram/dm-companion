namespace DmCompanion.Infrastructure;

public static class DirectoryCopy
{
    /// <summary>Copia source en target solo si target no existe. Devuelve false si ya existía.
    /// Copia en una carpeta temporal hermana y la renombra al final: nunca deja una copia a medias en target.</summary>
    public static async Task<bool> CopyIfMissingAsync(string source, string target, CancellationToken ct = default)
    {
        if (Directory.Exists(target)) return false;

        var temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await CopyTreeAsync(source, temp, ct);
            // Si otro proceso creó target entretanto, Move falla y la copia temporal se descarta.
            Directory.Move(temp, target);
            return true;
        }
        catch
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
            throw;
        }
    }

    private static async Task CopyTreeAsync(string source, string target, CancellationToken ct)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await input.CopyToAsync(output, ct);
        }
    }
}
