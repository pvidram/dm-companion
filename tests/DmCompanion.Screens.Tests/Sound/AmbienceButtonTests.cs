using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Components.Sound;

namespace DmCompanion.Screens.Tests.Sound;

public sealed class AmbienceButtonTests : SoundUiTestBase
{
    private IRenderedComponent<NoteReader> RenderNote(string path) => Render<NoteReader>(p => p.Add(r => r.Entity, Note(path)));

    private IRenderedComponent<AmbienceButton> RenderButton(string value) => Render<AmbienceButton>(p => p.Add(b => b.Value, value));

    [Fact]
    public void Ambience_button_absent_without_frontmatter()
    {
        var cut = RenderNote("Sin ambiente.md");

        cut.FindAll("[data-testid=ambience-button]").Should().BeEmpty();
    }

    [Fact]
    public async Task Ambience_button_does_not_play_on_open()
    {
        await SaveSceneAsync("Taberna");
        RenderNote("Taberna del Cuervo.md");

        (Player.Channels.Count, Mixer.IsPlaying).Should().Be((0, false));
    }

    [Fact]
    public async Task Ambience_button_shows_the_label_from_the_note()
    {
        await SaveSceneAsync("Taberna");
        var cut = RenderNote("Taberna del Cuervo.md");

        cut.Find("[data-testid=ambience-button]").TextContent.Should().Contain("Ambiente: Taberna");
    }

    [Theory]
    [InlineData("Taberna", "scene")]
    [InlineData("Bosque", "section")]
    [InlineData("fondo.mp3", "track")]
    public async Task Ambience_button_launches_scene_section_or_track(string value, string kind)
    {
        await SaveSceneAsync("Taberna");
        var cut = RenderButton(value);

        await ClickAsync(cut, "[data-testid=ambience-button]");

        cut.WaitForAssertion(() => Describe().Should().Be(kind), Timeout);
    }

    private string Describe() => Mixer.State switch
    {
        { SceneName: not null } => "scene",
        { Background.Source: SectionBackground } => "section",
        { Background.Source: TrackBackground } => "track",
        _ => "none",
    };

    [Fact]
    public void Ambience_not_found_is_struck_and_disabled()
    {
        var cut = RenderButton("Inexistente");

        var button = cut.Find("[data-testid=ambience-button]");
        (button.HasAttribute("disabled"), button.QuerySelector("s") is not null, button.TextContent.Contains("no encontrado")).Should().Be((true, true, true));
    }

    [Fact]
    public async Task Ambience_button_shows_playing()
    {
        await SaveSceneAsync("Taberna");
        await Mixer.LaunchSceneAsync((await Store.LoadSceneAsync("Taberna"))!);
        var cut = RenderButton("Taberna");

        cut.FindAll("[data-testid=ambience-playing]").Should().ContainSingle();
    }

    [Fact]
    public void Ambience_button_resolves_a_wikilink_to_a_track()
    {
        var cut = RenderButton("[[fondo.mp3]]");

        cut.Find("[data-testid=ambience-button]").TextContent.Should().Contain("Ambiente: fondo");
    }

    [Fact]
    public async Task Ambience_button_does_not_show_playing_while_paused()
    {
        await SaveSceneAsync("Taberna");
        await Mixer.LaunchSceneAsync((await Store.LoadSceneAsync("Taberna"))!);
        await Mixer.PauseAllAsync();
        var cut = RenderButton("Taberna");

        cut.FindAll("[data-testid=ambience-playing]").Should().BeEmpty();
    }

    [Fact]
    public async Task Ambience_button_reports_a_track_that_cannot_play()
    {
        var cut = RenderButton("fondo.mp3");
        Player.MissingPaths.Add("_audio/" + Fondo);

        await ClickAsync(cut, "[data-testid=ambience-button]");

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Should().Contain("No se encuentra: fondo"), Timeout);
    }

    [Fact]
    public async Task Ambience_button_reports_missing_scene_parts()
    {
        await Store.SaveSceneAsync(new AudioScene("Rota", null, [new SceneEffect("Nada/falta.mp3", 1, EffectRepeat.Loop)], 1.0), overwrite: false);
        var cut = RenderButton("Rota");

        await ClickAsync(cut, "[data-testid=ambience-button]");

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Should().Contain("Falta: Nada/falta.mp3"), Timeout);
    }

    [Fact]
    public async Task Ambience_button_follows_new_scenes()
    {
        var cut = RenderButton("Nueva");
        var before = cut.Find("[data-testid=ambience-button]").HasAttribute("disabled");

        await SaveSceneAsync("Nueva");

        cut.WaitForAssertion(() => (before, cut.Find("[data-testid=ambience-button]").HasAttribute("disabled")).Should().Be((true, false)), Timeout);
    }

    [Fact]
    public async Task Dispose_removes_the_subscriptions()
    {
        var cut = RenderButton("Taberna");
        cut.Dispose();
        var renders = cut.RenderCount;

        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        await Task.Delay(100);

        cut.RenderCount.Should().Be(renders);
    }
}
