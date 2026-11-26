using Bunit;
using DmCompanion.Screens.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalComponentBaseTests : JournalUiTestBase
{
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";

    private sealed class Probe : JournalComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, string.Join(",", Journal.Entries.Select(e => $"{e.Title}:{e.Images.Count(i => i.Exists)}")));
    }

    private sealed class UrlProbe : JournalComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, Journal.ImageUrl(Journal.Get(Faro)!.Images[0]) ?? "none");
    }

    protected override Task SeedAsync()
    {
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"]));
        return Task.CompletedTask;
    }

    [Fact]
    public void It_repaints_when_the_image_listing_finds_a_missing_image()
    {
        var cut = Render<Probe>();

        cut.WaitForAssertion(() => cut.Markup.Should().Be("Faro:0"), Wait);
    }

    [Fact]
    public void It_repaints_when_the_missing_image_arrives()
    {
        var cut = Render<UrlProbe>();
        cut.WaitForAssertion(() => cut.Markup.Should().Be("none"), Wait);

        WriteImage(TorvaldDiary + "/falta.png");

        // La URL con la fecha real del fichero solo sale del relistado, no del modo optimista («?v=0»).
        var expected = $"?v={File.GetLastWriteTimeUtc(Vault.PathOf(TorvaldDiary + "/falta.png")).Ticks}";
        cut.WaitForAssertion(() => cut.Markup.Should().EndWith(expected), Wait);
    }

    [Fact]
    public void It_is_available_when_the_index_is_registered() =>
        JournalComponentBase.IsAvailable(Services).Should().BeTrue();

    [Fact]
    public void It_is_not_available_without_the_index() =>
        JournalComponentBase.IsAvailable(new ServiceCollection().BuildServiceProvider()).Should().BeFalse();
}
