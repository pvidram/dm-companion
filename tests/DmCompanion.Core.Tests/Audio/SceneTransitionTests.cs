using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public class SceneTransitionTests
{
    private static readonly BackgroundSource Tavern = new SectionBackground("Música/Tabernas");
    private static readonly BackgroundSource Forest = new TrackBackground("Música/bosque.mp3");

    private static AudioScene Scene(BackgroundSource? background, params SceneEffect[] effects) =>
        new("S", background is null ? null : new SceneBackground(background, 0.7), effects, 0.9);

    private static SceneEffect Fx(string track, EffectRepeat? repeat = null) =>
        new(track, 0.5, repeat ?? EffectRepeat.Loop);

    private static PlayingEffect Playing(string id, string track, EffectRepeat repeat) => new(id, track, 0.5, repeat);

    [Fact]
    public void Background_changes_only_if_different()
    {
        var same = SceneTransition.Plan(new MixSnapshot(Tavern, []), Scene(new SectionBackground("música/tabernas")));
        var other = SceneTransition.Plan(new MixSnapshot(Tavern, []), Scene(Forest));

        (same.ChangeBackground, other.ChangeBackground).Should().Be((false, true));
    }

    [Fact]
    public void Scene_without_background_clears_the_current_one()
    {
        var clears = SceneTransition.Plan(new MixSnapshot(Tavern, []), Scene(null));
        var nothing = SceneTransition.Plan(new MixSnapshot(null, []), Scene(null));

        (clears.ChangeBackground, nothing.ChangeBackground).Should().Be((true, false));
    }

    [Fact]
    public void Background_starts_when_nothing_plays() =>
        SceneTransition.Plan(new MixSnapshot(null, []), Scene(Forest)).ChangeBackground.Should().BeTrue();

    [Fact]
    public void Looping_effects_not_in_scene_fade_out()
    {
        var current = new MixSnapshot(null,
            [Playing("1", "Fx/lluvia.mp3", EffectRepeat.Loop), Playing("2", "Fx/lobo.mp3", EffectRepeat.WithPause(5, 9))]);

        SceneTransition.Plan(current, Scene(null)).FadeOut.Should().BeEquivalentTo(["1", "2"]);
    }

    [Fact]
    public void Effects_in_scene_already_playing_are_kept()
    {
        var current = new MixSnapshot(null, [Playing("1", "Fx/Lluvia.mp3", EffectRepeat.Loop)]);
        var target = Fx("fx/lluvia.mp3", EffectRepeat.WithPause(2, 4));

        var plan = SceneTransition.Plan(current, Scene(null, target));

        plan.Keep.Should().BeEquivalentTo([("1", target)]);
    }

    [Fact]
    public void Missing_scene_effects_start()
    {
        var target = Fx("Fx/trueno.mp3");

        SceneTransition.Plan(new MixSnapshot(null, []), Scene(null, target)).Start.Should().BeEquivalentTo([target]);
    }

    [Fact]
    public void Once_effects_are_left_alone()
    {
        var current = new MixSnapshot(null, [Playing("1", "Fx/golpe.mp3", EffectRepeat.Once)]);

        var plan = SceneTransition.Plan(current, Scene(null));

        (plan.FadeOut, plan.Keep).Should().BeEquivalentTo((Array.Empty<string>(), Array.Empty<(string, SceneEffect)>()));
    }
}
