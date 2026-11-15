namespace DmCompanion.Core.Audio;

public enum RepeatKind { Once, Loop, RepeatWithPause }

/// <summary>Modo de repetición de un efecto; las pausas son segundos enteros y solo cuentan en <see cref="RepeatKind.RepeatWithPause"/>.</summary>
public sealed record EffectRepeat(RepeatKind Kind, int MinPauseSeconds = 0, int MaxPauseSeconds = 0)
{
    public static EffectRepeat Once { get; } = new(RepeatKind.Once);

    public static EffectRepeat Loop { get; } = new(RepeatKind.Loop);

    public static EffectRepeat WithPause(int min, int max) => new EffectRepeat(RepeatKind.RepeatWithPause, min, max).Normalize();

    public EffectRepeat Normalize()
    {
        if (Kind != RepeatKind.RepeatWithPause)
            return new EffectRepeat(Kind);

        var min = Math.Max(1, MinPauseSeconds);
        return new EffectRepeat(Kind, min, Math.Max(min, MaxPauseSeconds));
    }
}
