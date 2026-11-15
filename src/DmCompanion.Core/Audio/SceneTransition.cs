namespace DmCompanion.Core.Audio;

public sealed record PlayingEffect(string Id, string Track, double Volume, EffectRepeat Repeat);

public sealed record MixSnapshot(BackgroundSource? Background, IReadOnlyList<PlayingEffect> Effects);

public sealed record SceneTransitionPlan(
    bool ChangeBackground,
    IReadOnlyList<string> FadeOut,
    IReadOnlyList<(string Id, SceneEffect Target)> Keep,
    IReadOnlyList<SceneEffect> Start);

public static class SceneTransition
{
    /// <summary>
    /// Qué hay que hacer para pasar de la mezcla actual a la escena. Una escena es la mezcla entera: sin fondo, apaga el que suena.
    /// Los efectos de una vez en curso no entran en ninguna lista: terminan solos.
    /// </summary>
    public static SceneTransitionPlan Plan(MixSnapshot current, AudioScene scene)
    {
        var changeBackground = scene.Background is { } background
            ? !SameSource(current.Background, background.Source)
            : current.Background is not null;

        var running = current.Effects.Where(e => e.Repeat.Kind != RepeatKind.Once).ToList();
        var keep = new List<(string Id, SceneEffect Target)>();
        var start = new List<SceneEffect>();
        var matched = new HashSet<string>();

        foreach (var target in scene.Effects)
        {
            var existing = running.FirstOrDefault(e => !matched.Contains(e.Id) && SameTrack(e.Track, target.Track));
            if (existing is null)
                start.Add(target);
            else
            {
                matched.Add(existing.Id);
                keep.Add((existing.Id, target));
            }
        }

        var fadeOut = running.Where(e => !matched.Contains(e.Id)).Select(e => e.Id).ToList();
        return new SceneTransitionPlan(changeBackground, fadeOut, keep, start);
    }

    private static bool SameTrack(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SameSource(BackgroundSource? a, BackgroundSource b) => (a, b) switch
    {
        (TrackBackground x, TrackBackground y) => SameTrack(x.Track, y.Track),
        (SectionBackground x, SectionBackground y) => SameTrack(x.Section, y.Section),
        _ => false,
    };
}
