namespace DmCompanion.Screens.Platform;

/// <summary>
/// Consulta opcional de la última versión publicada. Solo la registra el host de escritorio; Screens la pide con GetService.
/// </summary>
public interface IUpdateCheck
{
    /// <summary>La última release publicada, o null si no se pudo saber (sin red, error, respuesta ilegible).</summary>
    Task<AvailableUpdate?> GetLatestAsync(CancellationToken ct);

    /// <summary>Abre la página de la release en el navegador del sistema.</summary>
    void OpenReleasePage(AvailableUpdate update);
}
