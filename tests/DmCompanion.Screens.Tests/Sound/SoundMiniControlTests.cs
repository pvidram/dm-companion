using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Screens.Components.Sound;

namespace DmCompanion.Screens.Tests.Sound;

public sealed class SoundMiniControlTests : SoundUiTestBase
{
    [Fact]
    public async Task Mini_control_when_silent_shows_only_the_icon_that_opens_the_panel()
    {
        var cut = Render<SoundMiniControl>();

        await ClickAsync(cut, "[data-testid=sound-mini-open]");

        (Shell.SoundQuickOpen, cut.FindAll("[data-testid=sound-mini-pause]").Count, cut.FindAll("[data-testid=sound-mini-name]").Count)
            .Should().Be((true, 0, 0));
    }

    [Fact]
    public async Task Mini_control_volume_applies_on_change()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        var cut = Render<SoundMiniControl>();

        cut.Find("[data-testid=sound-mini-volume]").Change("40");

        cut.WaitForAssertion(() => Mixer.State.MasterVolume.Should().BeApproximately(0.4, 0.001), Timeout);
    }

    [Fact]
    public async Task Mini_control_shows_scene_name_and_pauses()
    {
        await SaveSceneAsync("Taberna");
        await Mixer.LaunchSceneAsync((await Store.LoadSceneAsync("Taberna"))!);
        var cut = Render<SoundMiniControl>();
        var name = cut.Find("[data-testid=sound-mini-name]").TextContent.Trim();

        await ClickAsync(cut, "[data-testid=sound-mini-pause]");

        cut.WaitForAssertion(() => (name, Mixer.State.Paused).Should().Be(("Taberna", true)), Timeout);
    }

    [Fact]
    public async Task Mini_control_stays_visible_while_paused()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        await Mixer.PauseAllAsync();
        var cut = Render<SoundMiniControl>();

        cut.Find("[data-testid=sound-mini-pause]").GetAttribute("aria-label").Should().Be("Reanudar el sonido");
    }

    [Fact]
    public async Task Mini_control_name_opens_the_quick_panel()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Fondo));
        var cut = Render<SoundMiniControl>();

        await ClickAsync(cut, "[data-testid=sound-mini-name]");

        Shell.SoundQuickOpen.Should().BeTrue();
    }
}

public sealed class SoundMiniControlEmptyTests : SoundUiTestBase
{
    protected override bool WithAudio => false;

    [Fact]
    public void Mini_control_hidden_when_there_is_nothing_to_launch()
    {
        var cut = Render<SoundMiniControl>();

        cut.FindAll("[data-testid=sound-mini]").Should().BeEmpty();
    }
}
