using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests.Sound;

public sealed class SoundPanelTests : SoundTestContext
{
    [Fact]
    public void Rail_button_opens_the_sound_panel()
    {
        var cut = Render<Components.DmShell>();

        ClickWhenReady(cut, "button[aria-label='Sonido']");

        Shell.ActivePanel.Should().Be(OverlayPanel.Sound);
    }

    [Fact]
    public void Tapping_a_track_plays_it_once_as_effect()
    {
        var cut = RenderOpenSound();
        Expand(cut, "Efectos");

        ClickWhenReady(cut, "[data-testid=sound-track] .ds-list-row");

        cut.WaitForAssertion(() => Mixer.State.Effects.Select(e => (e.Track, e.Repeat.Kind)).Should().Equal((Trueno, RepeatKind.Once)), Wait);
    }

    [Fact]
    public void Background_button_sets_the_track_as_background()
    {
        var cut = RenderOpenSound();
        Expand(cut, "Bosque");

        ClickWhenReady(cut, "button[aria-label='Fondo: lluvia']");

        cut.WaitForAssertion(() => Mixer.State.Background!.Source.Should().Be(new TrackBackground(Lluvia)), Wait);
    }

    [Fact]
    public void Section_plays_as_background_list()
    {
        var cut = RenderOpenSound();

        ClickWhenReady(cut, "button[aria-label='Lista como fondo: Taberna']");

        cut.WaitForAssertion(() => Mixer.State.Background!.Source.Should().Be(new SectionBackground("Taberna")), Wait);
    }

    [Fact]
    public void Search_filters_tracks()
    {
        var cut = RenderOpenSound();

        cut.Find(".sound-block input[type=search]").Input("trueno");

        cut.WaitForAssertion(() => cut.FindAll("[data-testid=sound-track]").Select(row => row.QuerySelector(".ds-list-row")!.TextContent.Trim())
            .Should().Equal("trueno"), Wait);
    }

    [Fact]
    public void Star_toggles_favorite()
    {
        var cut = RenderOpenSound();
        Expand(cut, "Efectos");

        ClickWhenReady(cut, "button[aria-label='Favorito: trueno']");

        cut.WaitForAssertion(() => Store.Favorites.Should().Equal(Trueno), Wait);
    }

    [Fact]
    public void Star_shows_as_pressed_after_toggling()
    {
        var cut = RenderOpenSound();
        Expand(cut, "Efectos");

        ClickWhenReady(cut, "button[aria-label='Favorito: trueno']");

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Favorito: trueno']").GetAttribute("aria-pressed").Should().Be("true"), Wait);
    }

    [Fact]
    public async Task Repeat_with_pause_fields_update_the_effect()
    {
        await Mixer.PlayEffectAsync(Trueno);
        var cut = RenderOpenSound();

        cut.Find("select[data-testid=effect-mode]").Change("2");
        cut.WaitForAssertion(() => cut.Find("[data-testid=pause-max] input"), Wait);
        cut.Find("[data-testid=pause-max] input").Change("20");

        cut.WaitForAssertion(() => Mixer.State.Effects.Single().Repeat.Should().Be(EffectRepeat.WithPause(5, 20)), Wait);
    }

    [Fact]
    public async Task Missing_parts_are_reported()
    {
        await SaveSceneAsync("Cueva", new AudioScene("Cueva", null, [new SceneEffect("Efectos/no_existe.wav", 0.5, EffectRepeat.Loop)], 1.0));
        var cut = RenderOpenSound();

        cut.WaitForAssertion(() => cut.Find("[data-testid=scene-row] .ds-list-row"), Wait);
        cut.Find("[data-testid=scene-row] .ds-list-row").Click();

        cut.WaitForAssertion(() => cut.Find(".ds-notice--warning").TextContent.Should().Contain("Falta: Efectos/no_existe.wav"), Wait);
    }
}
