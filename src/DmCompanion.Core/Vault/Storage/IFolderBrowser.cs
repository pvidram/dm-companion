namespace DmCompanion.Core.Vault.Storage;

public interface IFolderBrowser
{
    IReadOnlyList<string> ListSubdirectories(string path);

    // Por defecto true: los dobles de test existentes siguen compilando.
    bool DirectoryExists(string path) => true;

    /// <summary>
    /// Si la carpeta parece un vault de Obsidian: no es la raíz de una unidad y tiene notas .md o una carpeta .obsidian
    /// (búsqueda acotada). Por defecto true, como DirectoryExists.
    /// </summary>
    bool LooksLikeVault(string path) => true;
}
