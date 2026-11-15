namespace DmCompanion.Core.Audio;

public static class AudioDefaults
{
    public static readonly TimeSpan Crossfade = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan StopFade = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan FadeStep = TimeSpan.FromMilliseconds(50);

    public const double DefaultVolume = 0.8;
}

public static class Fade
{
    /// <summary>Volumen lineal entre <paramref name="from"/> y <paramref name="to"/>; fuera de [0, duration] se acota.</summary>
    public static double VolumeAt(double from, double to, TimeSpan elapsed, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return to;

        var progress = Math.Clamp(elapsed / duration, 0.0, 1.0);
        return from + (to - from) * progress;
    }
}
