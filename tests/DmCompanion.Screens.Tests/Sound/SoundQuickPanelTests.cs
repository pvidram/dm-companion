using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Screens.Components.Sound;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests.Sound;

public sealed class SoundQuickPanelTests : SoundUiTestBase
{
    private async Task<IRenderedComponent<SoundQuickPanel>> OpenPanelAsync()
    {
        await SaveSceneAsync("Taberna");
        await Store.ToggleFavoriteAsync(Cuervo);
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        var cut = Render<SoundQuickPanel>();
        await cut.InvokeAsync(Shell.ToggleSoundQuick);
        return cut;
    }

    [Fact]
    public async Task Quick_panel_lists_favorites_and_scenes()
    {
        var cut = await OpenPanelAsync();

        (cut.Find("[data-testid=sound-favorite]").TextContent.Trim(), cut.Find("[data-testid=sound-scene]").TextContent.Trim())
            .Should().Be(("cuervo", "Taberna"));
    }

    [Fact]
    public async Task Favorite_plays_as_effect()
    {
        var cut = await OpenPanelAsync();

        await ClickAsync(cut, "[data-testid=sound-favorite]");

        cut.WaitForAssertion(() => Player.Channels.Values.Should().ContainSingle(c => c.Path == "_audio/" + Cuervo && !c.Loop), Timeout);
    }

    [Fact]
    public async Task Scene_launches_from_the_panel()
    {
        var cut = await OpenPanelAsync();

        await ClickAsync(cut, "[data-testid=sound-scene]");

        cut.WaitForAssertion(() => Mixer.State.SceneName.Should().Be("Taberna"), Timeout);
    }

    [Fact]
    public async Task Quick_panel_closes_when_tapping_outside()
    {
        var cut = await OpenPanelAsync();

        await ClickAsync(cut, "[data-testid=sound-quick-backdrop]");

        cut.FindAll("[data-testid=sound-quick]").Should().BeEmpty();
    }

    [Fact]
    public async Task Quick_panel_stays_open_when_the_mix_goes_silent()
    {
        var cut = await OpenPanelAsync();

        await Mixer.StopAllAsync();
        await Mixer.PauseAllAsync();
        await Mixer.ResumeAllAsync();

        cut.FindAll("[data-testid=sound-quick]").Should().ContainSingle();
    }

    [Fact]
    public async Task Missing_scene_part_is_reported()
    {
        await Store.SaveSceneAsync(new AudioScene("Rota", null, [new SceneEffect("Nada/falta.mp3", 1, EffectRepeat.Loop)], 1.0), overwrite: false);
        var cut = await OpenPanelAsync();

        await cut.InvokeAsync(() => cut.FindAll("[data-testid=sound-scene]").Single(b => b.TextContent.Contains("Rota")).Click());

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Should().Contain("Falta: Nada/falta.mp3"), Timeout);
    }

    [Fact]
    public async Task Favorite_that_cannot_play_is_reported()
    {
        var cut = await OpenPanelAsync();
        Player.MissingPaths.Add("_audio/" + Cuervo);

        await ClickAsync(cut, "[data-testid=sound-favorite]");

        cut.WaitForAssertion(() => cut.Find(".ds-notice").TextContent.Should().Contain("No se encuentra: cuervo"), Timeout);
    }

    [Fact]
    public async Task Notice_expires()
    {
        var cut = await OpenPanelAsync();
        Player.MissingPaths.Add("_audio/" + Cuervo);
        await ClickAsync(cut, "[data-testid=sound-favorite]");
        cut.WaitForAssertion(() => cut.FindAll(".ds-notice").Should().ContainSingle(), Timeout);

        Time.Advance(TimeSpan.FromSeconds(7));

        cut.WaitForAssertion(() => cut.FindAll(".ds-notice").Should().BeEmpty(), Timeout);
    }

    private PanelLayoutStore Layout => Services.GetRequiredService<PanelLayoutStore>();
    private FakeWindowFrameInterop Frames => Services.GetRequiredService<FakeWindowFrameInterop>();

    [Fact]
    public async Task Pinned_sound_quick_has_no_backdrop()
    {
        var cut = await OpenPanelAsync();

        await ClickAsync(cut, "[data-testid=sound-quick] button[aria-label=Anclar]");

        (Layout.IsPinned(LayoutKeys.SoundQuick), cut.FindAll("[data-testid=sound-quick-backdrop]").Count).Should().Be((true, 0));
    }

    [Fact]
    public async Task Pinned_sound_quick_reopens_at_its_anchor()
    {
        var anchor = new WindowAnchor(WindowCorner.BottomLeft, 10, 20);
        Layout.SetPinned(LayoutKeys.SoundQuick, true);
        Layout.SetAnchor(LayoutKeys.SoundQuick, anchor);

        await OpenPanelAsync();

        Frames.Attached.Last().Anchor.Should().Be(anchor);
    }

    [Fact]
    public async Task Unpinning_sound_quick_forgets_the_anchor()
    {
        Layout.SetPinned(LayoutKeys.SoundQuick, true);
        Layout.SetAnchor(LayoutKeys.SoundQuick, new WindowAnchor(WindowCorner.TopLeft, 5, 6));
        var cut = await OpenPanelAsync();

        await ClickAsync(cut, "[data-testid=sound-quick] button[aria-label=Desanclar]");

        (Layout.IsPinned(LayoutKeys.SoundQuick), Layout.GetAnchor(LayoutKeys.SoundQuick)).Should().Be((false, null));
    }

    [Fact]
    public async Task Closing_sound_quick_also_unpins_it()
    {
        Layout.SetPinned(LayoutKeys.SoundQuick, true);
        var cut = await OpenPanelAsync();

        await ClickAsync(cut, "[data-testid=sound-quick] button[aria-label=Cerrar]");

        (Layout.IsPinned(LayoutKeys.SoundQuick), Shell.SoundQuickOpen).Should().Be((false, false));
    }
}
