using Android.App;
using Android.Content;

namespace DmCompanion.App;

/// <summary>
/// Notificación del servicio de sonido: texto genérico (el reproductor no conoce nombres de escena)
/// y acciones Pausar/Reanudar y Parar, que llegan a <see cref="AudioPlaybackService"/>.
/// </summary>
public static class AudioNotification
{
    public const int Id = 6001;
    private const string ChannelId = "dm-companion-sound";

    public static void EnsureChannel(Context context)
    {
        if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager) return;
        if (manager.GetNotificationChannel(ChannelId) is not null) return;
        var channel = new NotificationChannel(ChannelId, "Sonido", NotificationImportance.Low);
        channel.SetSound(null, null);
        channel.EnableVibration(false);
        manager.CreateNotificationChannel(channel);
    }

    public static Notification Build(Context context, bool paused)
    {
        var toggle = paused
            ? Action(context, Android.Resource.Drawable.IcMediaPlay, "Reanudar", AudioPlaybackService.ActionResume, 1)
            : Action(context, Android.Resource.Drawable.IcMediaPause, "Pausar", AudioPlaybackService.ActionPause, 1);
        var stop = Action(context, Android.Resource.Drawable.IcMenuCloseClearCancel, "Parar", AudioPlaybackService.ActionStop, 2);

        var builder = new Notification.Builder(context, ChannelId)
            .SetSmallIcon(paused ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay)!
            .SetContentTitle("DM Companion")!
            .SetContentText(paused ? "En pausa" : "Sonando")!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetCategory(Notification.CategoryTransport)!
            .AddAction(toggle)!
            .AddAction(stop)!;

        if (OpenAppIntent(context) is { } open) builder.SetContentIntent(open);
        return builder.Build()!;
    }

    /// <summary>Actualiza la notificación ya mostrada por el servicio (sin permiso, Android la ignora).</summary>
    public static void Update(Context context, bool paused)
    {
        EnsureChannel(context);
        if (context.GetSystemService(Context.NotificationService) is NotificationManager manager)
            manager.Notify(Id, Build(context, paused));
    }

    private static Notification.Action Action(Context context, int icon, string title, string action, int requestCode)
    {
        var intent = new Intent(context, typeof(AudioPlaybackService)).SetAction(action);
        var pending = PendingIntent.GetService(context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        return new Notification.Action.Builder(Android.Graphics.Drawables.Icon.CreateWithResource(context, icon), title, pending).Build()!;
    }

    private static PendingIntent? OpenAppIntent(Context context)
    {
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
        if (launch is null) return null;
        launch.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
        return PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }
}
