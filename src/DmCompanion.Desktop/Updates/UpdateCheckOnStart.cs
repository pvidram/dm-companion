using DmCompanion.Screens.Services;

namespace DmCompanion.Desktop.Updates;

/// <summary>Lanza la consulta de versiones una vez, en segundo plano, al arrancar el host.</summary>
public sealed class UpdateCheckOnStart(UpdateNotice notice, ILogger<UpdateCheckOnStart> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await notice.RefreshAsync(AppVersion.Current, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        // Captura amplia a propósito: una función opcional nunca debe parar el host (StopHost por defecto).
        catch (Exception ex)
        {
            logger.LogWarning("Falló la comprobación de versiones al arrancar ({ExceptionType}).", ex.GetType().Name);
        }
    }
}
