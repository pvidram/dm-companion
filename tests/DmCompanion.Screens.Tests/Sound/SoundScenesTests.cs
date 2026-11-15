using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests.Sound;

public sealed class SoundScenesTests : SoundTestContext
{
    [Fact]
    public async Task Save_scene_captures_the_mix()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Lluvia));
        var cut = RenderOpenSound();

        cut.FindAll("button").First(b => b.TextContent.Contains("Guardar como escena")).Click();
        OpenDialog(cut).QuerySelector("input")!.Input("Bosque");
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Guardar").Click();

        cut.WaitForAssertion(() => Store.LoadSceneAsync("Bosque").GetAwaiter().GetResult()!.Background!.Source.Should().Be(new TrackBackground(Lluvia)), Wait);
    }

    [Fact]
    public async Task Rename_warns_about_citing_notes()
    {
        await SaveSceneAsync("Taberna");
        var cut = RenderOpenSound();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Renombrar Taberna']"), Wait);
        cut.Find("button[aria-label='Renombrar Taberna']").Click();

        OpenDialog(cut).TextContent.Should().Contain("2 notas la citan y mostrarán «no encontrado»");
    }

    [Fact]
    public async Task Delete_asks_for_confirmation()
    {
        await SaveSceneAsync("Bosque");
        var cut = RenderOpenSound();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Borrar Bosque']"), Wait);

        cut.Find("button[aria-label='Borrar Bosque']").Click();
        var askedAndKept = OpenDialog(cut).TextContent.Contains("¿Borrar la escena «Bosque»?") && Store.SceneNames.Contains("Bosque");
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Borrar").Click();

        cut.WaitForAssertion(() => (askedAndKept, Store.SceneNames.Count).Should().Be((true, 0)), Wait);
    }

    [Fact]
    public async Task Store_failure_shows_an_error()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Lluvia));
        Write("_audio/_dm/scenes", "no soy una carpeta");
        var cut = RenderOpenSound();

        cut.FindAll("button").First(b => b.TextContent.Contains("Guardar como escena")).Click();
        OpenDialog(cut).QuerySelector("input")!.Input("Bosque");
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Guardar").Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("[role=alert]")!.TextContent.Should().Contain("No se pudo escribir"), Wait);
    }
}
