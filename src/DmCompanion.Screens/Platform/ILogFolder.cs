namespace DmCompanion.Screens.Platform;

/// <summary>
/// Acceso opcional a la carpeta de registros. Solo lo registra el host de escritorio; Screens lo pide con GetService.
/// </summary>
public interface ILogFolder
{
    bool IsAvailable { get; }

    /// <summary>Abre la carpeta; false si el sistema no pudo hacerlo (el motivo queda en el registro).</summary>
    bool Open();
}
