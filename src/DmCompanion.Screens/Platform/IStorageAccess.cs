namespace DmCompanion.Screens.Platform;

public interface IStorageAccess
{
    bool HasAllFilesAccess { get; }
    string DefaultBrowseRoot { get; }
    void RequestAllFilesAccess();

    /// <summary>El explorador ofrece «Pegar una ruta» y puede salir de la raíz.</summary>
    bool CanTypePath => false;

    /// <summary>Raíces adicionales (unidades). Vacío: no se muestran.</summary>
    IReadOnlyList<string> Roots => [];

    /// <summary>Carpeta donde empieza el explorador; null: la raíz.</summary>
    string? InitialPath => null;
}
