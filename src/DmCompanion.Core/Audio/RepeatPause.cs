namespace DmCompanion.Core.Audio;

public static class RepeatPause
{
    /// <summary>Pausa entera uniforme en [min, max] segundos.</summary>
    public static TimeSpan Next(EffectRepeat repeat, Random random)
    {
        var normalized = repeat.Normalize();
        return TimeSpan.FromSeconds(random.Next(normalized.MinPauseSeconds, normalized.MaxPauseSeconds + 1));
    }
}
