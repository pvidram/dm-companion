using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DmCompanion.Screens.Platform;

namespace DmCompanion.App;

/// <summary>
/// Servicio en primer plano de tipo reproducción: mantiene vivo el proceso con la pantalla apagada y muestra
/// la notificación. No reproduce nada: lo hace <see cref="AndroidAudioPlayer"/>, al que reenvía las acciones.
/// </summary>
[Service(Name = "es.pauvidal.dmcompanion.AudioPlaybackService", Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class AudioPlaybackService : Service
{
    public const string ActionStart = "es.pauvidal.dmcompanion.audio.START";
    public const string ActionPause = "es.pauvidal.dmcompanion.audio.PAUSE";
    public const string ActionResume = "es.pauvidal.dmcompanion.audio.RESUME";
    public const string ActionStop = "es.pauvidal.dmcompanion.audio.STOP";
    public const string ActionShutdown = "es.pauvidal.dmcompanion.audio.SHUTDOWN";

    private bool inForeground;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        switch (intent?.Action)
        {
            case ActionPause: Forward(AudioExternalAction.Pause); break;
            case ActionResume: Forward(AudioExternalAction.Resume); break;
            case ActionStop: Forward(AudioExternalAction.Stop); break;
            case ActionShutdown: Shutdown(startId); break;
            default: EnterForeground(); break;
        }

        // Si Android mata el proceso, no se recrea el servicio: la mezcla se ha perdido con él.
        return StartCommandResult.NotSticky;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        // Cerrar la app desde recientes para todo (spec §6.8.3).
        Forward(AudioExternalAction.Stop);
        base.OnTaskRemoved(rootIntent);
    }

    public override void OnDestroy()
    {
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    /// <summary>Arranque por StartForegroundService: hay que pasar a primer plano sí o sí.</summary>
    private void EnterForeground()
    {
        try
        {
            AudioNotification.EnsureChannel(this);
            StartForeground(AudioNotification.Id, AudioNotification.Build(this, Player()?.IsPaused ?? false), ForegroundService.TypeMediaPlayback);
            inForeground = true;
        }
        catch (Exception ex)
        {
            // Una excepción sin capturar en el servicio cerraría la app; sin primer plano el sonido sigue igual.
            Android.Util.Log.Warn(AndroidLogcatLoggerProvider.Tag, $"AudioPlaybackService: no pasa a primer plano: {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// La parada la hace el propio servicio: pararlo desde fuera antes de su startForeground cierra la app
    /// (Android 12+). Si queda un arranque posterior en cola, StopSelfResult no lo para y ese arranque vuelve
    /// a primer plano.
    /// </summary>
    private void Shutdown(int startId)
    {
        if (!inForeground) EnterForeground();
        StopForeground(StopForegroundFlags.Remove);
        inForeground = false;
        StopSelfResult(startId);
    }

    private static AndroidAudioPlayer? Player() =>
        IPlatformApplication.Current?.Services.GetService(typeof(IAudioPlayer)) as AndroidAudioPlayer;

    private static void Forward(AudioExternalAction action)
    {
        if (Player() is { } player) _ = player.HandleExternalActionAsync(action);
    }
}
