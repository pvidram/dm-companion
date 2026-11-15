using Android.Content;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

/// <summary>
/// Arranca, actualiza y para <see cref="AudioPlaybackService"/> y pide el permiso de notificaciones la primera vez.
/// Cuando los canales se acaban solos (fin de pista, pausa de un efecto que se repite) el servicio espera
/// <see cref="IdleGrace"/> antes de pararse: Android no deja volver a arrancarlo con la app en segundo plano
/// (pantalla apagada), y la mezcla suele lanzar la siguiente pista enseguida.
/// </summary>
public sealed class AudioPlaybackSession(ILogger logger)
{
    public static readonly TimeSpan IdleGrace = TimeSpan.FromMinutes(5);
    private const string NotificationsDeniedKey = "audio.notifications.denied";

    private readonly Lock gate = new();
    private bool running;
    private bool notificationPermissionAsked;
    private CancellationTokenSource? idleStop;
    private Android.OS.PowerManager.WakeLock? cpu;

    private static Context Context => Android.App.Application.Context;

    /// <summary>Asegura el servicio en primer plano y deja la notificación en el estado indicado.</summary>
    public void Show(bool paused)
    {
        bool start;
        lock (gate)
        {
            CancelIdleStop();
            start = !running;
            running = true;
        }

        // MediaPlayer solo mantiene la CPU despierta mientras suena: sin esto, la espera de un efecto «repetir con pausa»
        // se dormiría con la pantalla apagada. En pausa global las esperas están congeladas y no hace falta.
        if (paused) ReleaseCpu();
        else HoldCpu();
        if (start) Start();
        else AudioNotification.Update(Context, paused);
        RequestNotificationPermissionOnce();
    }

    /// <summary>Programa la parada del servicio si nada vuelve a sonar dentro del margen.</summary>
    public void StopWhenIdle()
    {
        CancellationToken token;
        lock (gate)
        {
            if (!running) return;
            CancelIdleStop();
            idleStop = new CancellationTokenSource();
            token = idleStop.Token;
        }

        _ = StopAfterGraceAsync(token);
    }

    public void StopNow()
    {
        ReleaseCpu();
        lock (gate)
        {
            CancelIdleStop();
            if (!running) return;
            running = false;
        }

        try
        {
            // StartService (no StopService): el servicio garantiza su startForeground antes de pararse.
            Context.StartService(new Intent(Context, typeof(AudioPlaybackService)).SetAction(AudioPlaybackService.ActionShutdown));
        }
        catch (Exception ex)
        {
            logger.LogWarning("No se pudo parar el servicio de sonido: {Error}", ex.GetType().Name);
        }
    }

    private void Start()
    {
        try
        {
            // El servicio lee el estado de pausa del reproductor al pasar a primer plano, no el de este momento.
            var intent = new Intent(Context, typeof(AudioPlaybackService)).SetAction(AudioPlaybackService.ActionStart);
            Context.StartForegroundService(intent);
        }
        catch (Exception ex)
        {
            // Típico: ForegroundServiceStartNotAllowedException con la app en segundo plano. Suena igual,
            // pero Android puede cortarlo con la pantalla apagada.
            lock (gate) running = false;
            ReleaseCpu();
            logger.LogWarning("No se pudo arrancar el servicio de sonido: {Error}", ex.GetType().Name);
        }
    }

    private async Task StopAfterGraceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(IdleGrace, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        StopNow();
    }

    // Sin contar referencias: varias llamadas seguidas retienen un único wake lock que se suelta de una vez.
    private void HoldCpu()
    {
        try
        {
            lock (gate)
            {
                cpu ??= (Context.GetSystemService(Context.PowerService) as Android.OS.PowerManager)?.NewWakeLock(Android.OS.WakeLockFlags.Partial, "DmCompanion:audio");
                cpu?.SetReferenceCounted(false);
                if (cpu is { IsHeld: false } idle) idle.Acquire();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("No se pudo retener la CPU para el sonido: {Error}", ex.GetType().Name);
        }
    }

    private void ReleaseCpu()
    {
        try
        {
            lock (gate)
            {
                if (cpu is { IsHeld: true } held) held.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("No se pudo soltar la CPU del sonido: {Error}", ex.GetType().Name);
        }
    }

    private void CancelIdleStop()
    {
        idleStop?.Cancel();
        idleStop?.Dispose();
        idleStop = null;
    }

    private void RequestNotificationPermissionOnce()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        lock (gate)
        {
            if (notificationPermissionAsked) return;
            notificationPermissionAsked = true;
        }

        _ = RequestNotificationPermissionAsync();
    }

    private async Task RequestNotificationPermissionAsync()
    {
        try
        {
            // Si se deniega, el sonido sigue igual; solo falta la notificación visible. La negativa se recuerda
            // para no volver a pedirlo en cada arranque (se puede activar en los ajustes de Android).
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() == PermissionStatus.Granted) return;
                if (Preferences.Default.Get(NotificationsDeniedKey, false)) return;
                var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted) Preferences.Default.Set(NotificationsDeniedKey, true);
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning("No se pudo pedir el permiso de notificaciones: {Error}", ex.GetType().Name);
        }
    }
}
