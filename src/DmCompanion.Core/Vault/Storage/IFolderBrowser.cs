namespace DmCompanion.Core.Vault.Storage;

public enum FolderVaultStatus { Vault, DriveRoot, Empty, Unknown }

public enum CreateFolderStatus { Created, InvalidName, AlreadyExists, ParentMissing, Failed }

/// <summary>Resultado de crear una carpeta. Path: ruta completa si Created; Message: texto en español en cualquier otro estado.</summary>
public sealed record CreateFolderResult(CreateFolderStatus Status, string? Path = null, string? Message = null);

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

    /// <summary>
    /// Clasifica la carpeta: Vault (o inexistente), DriveRoot, Empty (sin notas) o Unknown (ilegible o con más entradas
    /// de las que se examinan).
    /// </summary>
    FolderVaultStatus Classify(string path) => LooksLikeVault(path) ? FolderVaultStatus.Vault : FolderVaultStatus.Unknown;

    CreateFolderResult CreateDirectory(string parentPath, string name) =>
        new(CreateFolderStatus.Failed, null, "Este explorador no puede crear carpetas.");
}
