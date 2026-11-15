using System.Reflection;
using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Screens.Components.Sound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests.Sound;

public sealed class SoundFixesTests : SoundTestContext
{
    private static AngleSharp.Dom.IElement SaveButton(IRenderedComponent<Components.DmShell> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Contains("Guardar como escena"));

    private static int Handlers(object source, string eventName) =>
        ((Delegate?)source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source))?.GetInvocationList().Length ?? 0;

    [Fact]
    public async Task Save_is_disabled_when_only_a_one_shot_plays()
    {
        await Mixer.PlayEffectAsync(Trueno);
        var cut = RenderOpenSound();

        SaveButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Overwrite_is_disabled_when_nothing_capturable_plays()
    {
        await SaveSceneAsync("Bosque");
        var cut = RenderOpenSound();
        cut.WaitForElement("button[aria-label='Sobrescribir Bosque con la mezcla actual']", Wait);

        cut.Find("button[aria-label='Sobrescribir Bosque con la mezcla actual']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Overwrite_asks_and_then_replaces_the_scene_with_the_mix()
    {
        await SaveSceneAsync("Bosque");
        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"));
        var cut = RenderOpenSound();
        cut.WaitForElement("button[aria-label='Sobrescribir Bosque con la mezcla actual']", Wait);

        cut.Find("button[aria-label='Sobrescribir Bosque con la mezcla actual']").Click();
        var asked = OpenDialog(cut).TextContent.Contains("¿Sustituir la escena «Bosque» por lo que suena ahora?");
        var untouched = (await Store.LoadSceneAsync("Bosque"))!.Background!.Source;
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Sustituir").Click();

        cut.WaitForAssertion(() => (asked, untouched, Store.LoadSceneAsync("Bosque").GetAwaiter().GetResult()!.Background!.Source)
            .Should().Be((true, new TrackBackground(Lluvia), new SectionBackground("Taberna"))), Wait);
    }

    [Fact]
    public async Task Mixer_changes_do_not_repaint_the_library()
    {
        var cut = RenderOpenSound();
        var library = cut.FindComponent<SoundLibrary>();
        var mix = cut.FindComponent<SoundMix>();
        var (libraryBefore, mixBefore) = (library.RenderCount, mix.RenderCount);

        await Mixer.SetMasterVolumeAsync(0.4);

        cut.WaitForAssertion(() => (library.RenderCount - libraryBefore, mix.RenderCount > mixBefore).Should().Be((0, true)), Wait);
    }

    [Fact]
    public async Task Blocks_unsubscribe_when_disposed()
    {
        // El mini control de la franja sigue escuchando: solo deben irse los bloques de la pantalla.
        var cut = RenderOpenSound();
        var open = Handlers(Mixer, "Changed");
        await cut.InvokeAsync(() => Shell.Toggle(Screens.Services.OverlayPanel.Sound));
        cut.WaitForAssertion(() => Handlers(Mixer, "Changed").Should().BeLessThan(open), Wait);
        var closed = Handlers(Mixer, "Changed");

        await cut.InvokeAsync(() => Shell.Toggle(Screens.Services.OverlayPanel.Sound));
        await cut.InvokeAsync(() => Shell.Toggle(Screens.Services.OverlayPanel.Sound));

        cut.WaitForAssertion(() => Handlers(Mixer, "Changed").Should().Be(closed), Wait);
    }

    [Fact]
    public async Task Renaming_the_playing_scene_keeps_it_highlighted_under_the_new_name()
    {
        await SaveSceneAsync("Bosque");
        await Mixer.LaunchSceneAsync((await Store.LoadSceneAsync("Bosque"))!);
        var cut = RenderOpenSound();
        cut.WaitForElement("button[aria-label='Renombrar Bosque']", Wait);

        cut.Find("button[aria-label='Renombrar Bosque']").Click();
        OpenDialog(cut).QuerySelector("input")!.Input("Selva");
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Renombrar").Click();

        cut.WaitForAssertion(() => Mixer.State.SceneName.Should().Be("Selva"), Wait);
    }

    [Fact]
    public async Task Renaming_the_playing_scene_to_an_unsafe_name_uses_the_stored_name()
    {
        await SaveSceneAsync("Bosque");
        await Mixer.LaunchSceneAsync((await Store.LoadSceneAsync("Bosque"))!);
        var cut = RenderOpenSound();
        cut.WaitForElement("button[aria-label='Renombrar Bosque']", Wait);

        cut.Find("button[aria-label='Renombrar Bosque']").Click();
        OpenDialog(cut).QuerySelector("input")!.Input("Cueva: noche");
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Renombrar").Click();

        cut.WaitForAssertion(() => Mixer.State.SceneName.Should().Be("Cueva noche"), Wait);
    }

    [Fact]
    public async Task Saved_scene_is_the_mix_from_when_the_dialog_opened()
    {
        await Mixer.SetBackgroundAsync(new TrackBackground(Lluvia));
        var cut = RenderOpenSound();
        SaveButton(cut).Click();
        OpenDialog(cut).QuerySelector("input")!.Input("Bosque");

        await Mixer.SetBackgroundAsync(new SectionBackground("Taberna"));
        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Guardar").Click();

        cut.WaitForAssertion(() => Store.LoadSceneAsync("Bosque").GetAwaiter().GetResult()!.Background!.Source.Should().Be(new TrackBackground(Lluvia)), Wait);
    }

    [Fact]
    public void Notices_expire()
    {
        Directory.CreateDirectory(PathOf("_audio/_dm/favorites.json"));
        var cut = RenderOpenSound();
        Expand(cut, "Efectos");
        cut.Find("button[aria-label='Favorito: trueno']").Click();
        cut.WaitForElement(".sound-block [role=alert]", Wait);

        ((FakeTimeProvider)Services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromSeconds(7));

        cut.WaitForAssertion(() => cut.FindAll(".sound-block [role=alert]").Should().BeEmpty(), Wait);
    }
}
