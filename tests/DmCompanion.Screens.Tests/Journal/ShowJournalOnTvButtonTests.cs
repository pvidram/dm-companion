using Bunit;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class ShowJournalOnTvButtonTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Vacia = LyraDiary + "/2026-11-12 Vacia.md";

    public ShowJournalOnTvButtonTests()
    {
        Services.AddSingleton<ITvServer>(new FakeTvServer());
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<TvMapFeed>();
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"]));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteFile(Vacia, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"]));
        return Task.CompletedTask;
    }

    private TvSession Tv => Services.GetRequiredService<TvSession>();

    private IRenderedComponent<ShowJournalOnTvButton> RenderButton(string path) =>
        Render<ShowJournalOnTvButton>(p => p.Add(c => c.Entry, Journal.Get(path)!));

    [Fact]
    public void It_is_hidden_while_the_tv_is_off() => RenderButton(Ankheg).FindAll("button").Should().BeEmpty();

    [Fact]
    public async Task It_appears_when_the_tv_is_turned_on()
    {
        var cut = RenderButton(Ankheg);

        await Tv.EnableAsync();

        cut.WaitForAssertion(() => cut.Find("button").TextContent.Should().Contain("Mostrar en TV"), Wait);
    }

    [Fact]
    public async Task It_is_hidden_for_an_entry_with_nothing_to_show()
    {
        await Journal.RefreshImagesAsync();
        await Tv.EnableAsync();

        RenderButton(Vacia).FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public async Task Clicking_it_plays_the_entry_on_the_tv()
    {
        await Tv.EnableAsync();
        var cut = RenderButton(Ankheg);

        cut.Find("button").Click();

        cut.WaitForAssertion(() => (Tv.Mode, Tv.Journal?.Title).Should().Be((TvMode.Content, "Lyra · Ankheg")), Wait);
    }
}
