namespace DmCompanion.Core.Audio;

public sealed record SceneBackground(BackgroundSource Source, double Volume);

public sealed record SceneEffect(string Track, double Volume, EffectRepeat Repeat);

public sealed record AudioScene(
    string Name,
    SceneBackground? Background,
    IReadOnlyList<SceneEffect> Effects,
    double MasterVolume);
