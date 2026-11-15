using Android.Content;
using Android.Media;

namespace DmCompanion.App;

/// <summary>
/// Foco de audio de la app (uso juego, contenido música). Ante una pérdida (llamada, otra app de música)
/// avisa por <c>onLost</c>; al recuperarlo no hace nada: tras una llamada el sonido queda en pausa.
/// </summary>
public sealed class AudioFocusHandler : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    private readonly AudioManager? audioManager;
    private readonly AudioFocusRequestClass request;
    private readonly Action onLost;
    private readonly Lock gate = new();
    private bool held;

    public AudioFocusHandler(Context context, Action onLost)
    {
        this.onLost = onLost;
        audioManager = context.GetSystemService(Context.AudioService) as AudioManager;
        var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Music)!
            .Build()!;
        request = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
            .SetAudioAttributes(attributes)!
            .SetOnAudioFocusChangeListener(this)!
            .Build()!;
    }

    /// <summary>Pide el foco si no lo tiene; false si el sistema lo niega (llamada en curso).</summary>
    public bool Request()
    {
        if (audioManager is null) return true;
        lock (gate)
        {
            if (!held) held = audioManager.RequestAudioFocus(request) == AudioFocusRequest.Granted;
            return held;
        }
    }

    public void Abandon()
    {
        if (audioManager is null) return;
        lock (gate)
        {
            if (!held) return;
            audioManager.AbandonAudioFocusRequest(request);
            held = false;
        }
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        // Con "CanDuck" el sistema baja el volumen solo (API 26+); no hace falta pausar.
        if (focusChange is AudioFocus.Loss or AudioFocus.LossTransient)
        {
            // También en la transitoria: así «Reanudar» durante la llamada vuelve a pedirlo.
            lock (gate) held = false;
            onLost();
        }
    }
}
