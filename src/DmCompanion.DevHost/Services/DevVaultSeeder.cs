using DmCompanion.Screens.Services;

namespace DmCompanion.DevHost.Services;

// Nunca toca el vault real: por defecto trabaja sobre una copia de samples/vault-demo
// y solo usa otra carpeta si DevHost:VaultPath está puesto de forma explícita.
public sealed class DevVaultSeeder(
    IConfiguration configuration,
    IHostEnvironment environment,
    VaultHost vaultHost,
    ILogger<DevVaultSeeder> logger)
{
    private string? ExplicitPath => configuration["DevHost:VaultPath"] is { Length: > 0 } path ? path : null;

    // DevHost:DefaultVaultPath solo mueve la copia por defecto (los tests no deben escribir en ~/.local/share).
    private string DefaultPath => configuration["DevHost:DefaultVaultPath"] is { Length: > 0 } path ? path : DevHostPaths.DefaultVault;

    public string VaultPath => ExplicitPath ?? DefaultPath;

    public async Task StartAsync()
    {
        if (ExplicitPath is null && !Directory.Exists(VaultPath)) Seed();

        if (await vaultHost.OpenAsync(VaultPath))
            logger.LogInformation("Vault abierto en {Path}", VaultPath);
        else
            logger.LogError("No se pudo abrir el vault: {Error}", vaultHost.LastError);
    }

    private void Seed()
    {
        var source = ResolveSeedSource();
        // Se copia a una carpeta temporal y se renombra: una copia a medias no debe quedar como vault.
        var staging = VaultPath + ".seeding";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(staging, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(staging, Path.GetRelativePath(source, file)));
        Directory.Move(staging, VaultPath);
        logger.LogInformation("Vault de demostración copiado de {Source} a {Target}", source, VaultPath);
    }

    private string ResolveSeedSource()
    {
        if (configuration["DevHost:SeedFrom"] is { Length: > 0 } configured)
        {
            return Directory.Exists(configured)
                ? configured
                : throw new DirectoryNotFoundException($"DevHost:SeedFrom apunta a «{configured}», que no existe.");
        }

        for (var dir = new DirectoryInfo(environment.ContentRootPath); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "vault-demo");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException(
            $"No se encuentra samples/vault-demo subiendo desde «{environment.ContentRootPath}». Indica la ruta en DevHost:SeedFrom.");
    }
}
