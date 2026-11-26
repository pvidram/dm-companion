using Bunit;
using DmCompanion.Screens.Components;

namespace DmCompanion.Screens.Tests;

public sealed class JournalThumbnailTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";
    private const string Foto = TorvaldDiary + "/2026-11-13 Foto.md";
    private const string Mixta = TorvaldDiary + "/2026-11-14 Mixta.md";

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"]));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"]));
        WriteFile(Foto, EntryText("2026-11-13T19:00:00+01:00", ["foto.heic"]));
        WriteImage(TorvaldDiary + "/foto.heic");
        WriteFile(Mixta, EntryText("2026-11-14T19:00:00+01:00", ["foto.heic", "buena.png"]));
        WriteImage(TorvaldDiary + "/buena.png");
        return Task.CompletedTask;
    }

    private IRenderedComponent<JournalThumbnail> RenderThumb(string path, Action<ComponentParameterCollectionBuilder<JournalThumbnail>>? more = null) =>
        Render<JournalThumbnail>(p =>
        {
            p.Add(c => c.Entry, Journal.Get(path)!);
            more?.Invoke(p);
        });

    [Fact]
    public void It_shows_the_first_image_that_can_be_shown_with_the_title_as_label()
    {
        var cut = RenderThumb(Ankheg);

        (cut.Find("img").GetAttribute("src")!.StartsWith("/_vault/Personajes/Lyra/Diario/", StringComparison.Ordinal), cut.Find(".journal-thumb__label").TextContent)
            .Should().Be((true, "Ankheg"));
    }

    [Fact]
    public async Task It_explains_a_missing_first_image()
    {
        await Journal.RefreshImagesAsync();

        var cut = RenderThumb(Faro);

        (cut.FindAll("img").Count, cut.Find(".journal-thumb__empty").TextContent).Should().Be((0, "Imagen no encontrada: falta.png"));
    }

    [Fact]
    public void It_explains_a_format_that_cannot_be_shown() =>
        RenderThumb(Foto).Find(".journal-thumb__empty").TextContent.Should().Be("Formato no compatible");

    [Fact]
    public void It_skips_an_image_that_cannot_be_shown_when_a_later_one_can()
    {
        var cut = RenderThumb(Mixta);

        cut.Find("img").GetAttribute("src")!.Should().Contain("buena.png");
    }

    [Fact]
    public void A_custom_caption_replaces_the_title() =>
        RenderThumb(Ankheg, p => p.Add(c => c.Caption, "Lyra")).Find(".journal-thumb__label").TextContent.Should().Be("Lyra");

    [Fact]
    public void It_can_hide_the_label() =>
        RenderThumb(Ankheg, p => p.Add(c => c.ShowLabel, false)).FindAll(".journal-thumb__label").Should().BeEmpty();

    [Fact]
    public void Without_a_callback_the_button_is_disabled() =>
        RenderThumb(Ankheg).Find("button").HasAttribute("disabled").Should().BeTrue();

    [Fact]
    public void Clicking_it_raises_OnSelect()
    {
        var clicks = 0;
        var cut = RenderThumb(Ankheg, p => p.Add(c => c.OnSelect, () => clicks++));

        cut.Find("button").Click();

        clicks.Should().Be(1);
    }
}
