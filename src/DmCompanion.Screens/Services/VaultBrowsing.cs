namespace DmCompanion.Screens.Services;

/// <summary>Carpeta elegida en el explorador y si parece un vault de Obsidian (si no, se pide confirmación).</summary>
public sealed record VaultChoice(string Path, bool LooksLikeVault, bool CanCreateVault = false);

/// <summary>Cambio de vault desde el panel «Cambiar vault», compartido por el panel y su diálogo de confirmación.</summary>
public static class VaultBrowsing
{
    public const string UnlikelyVaultMessage =
        "Esta carpeta no parece un vault de Obsidian: es la raíz de una unidad o no tiene notas .md ni carpeta .obsidian. Si la abres, se indexará y se vigilará todo su contenido.";

    /// <summary>
    /// Abre <paramref name="path"/> en lugar del vault actual, tras confirmar los cambios sin guardar del editor.
    /// El vault actual solo se suelta si el nuevo se abre; si no, sigue abierto y el panel muestra el error.
    /// </summary>
    public static Task SwitchAsync(VaultHost host, ShellState shell, string path) =>
        shell.RequestNavigationAsync(async () =>
        {
            shell.Reset();
            if (!await host.OpenAsync(path))
                shell.ShowVaultSwitchError(host.LastError ?? $"No se puede abrir el vault en «{path}».");
        });

    public static bool IsCurrent(VaultHost host, string path) =>
        host.Current is { } open && string.Equals(open.RootPath.TrimEnd('/', '\\'), path.TrimEnd('/', '\\'), StringComparison.Ordinal);
}
