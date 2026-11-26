using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class GlossaryDetailJournalTests : JournalUiTestBase
{
    private const string AnkhegKey = "srd:criaturas/ankheg";
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";

    public GlossaryDetailJournalTests() => Services.AddSingleton<MapSession>();

    protected override Task SeedAsync()
    {
        WriteFile("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n");
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"], AnkhegKey));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteFile(LyraDiary + "/2026-11-10 Lobo.md", EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"], "[[Lobo]]"));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        return Task.CompletedTask;
    }

    private IRenderedComponent<GlossaryDetail> RenderDetail(string key) => Render<GlossaryDetail>(p => p.Add(c => c.Key, key));

    [Fact]
    public void An_srd_creature_sheet_has_the_section_after_its_stats()
    {
        var cut = RenderDetail(AnkhegKey);

        (string.Join("|", cut.FindAll(".glossary-detail .journal-creature h4").Select(h => h.TextContent)), cut.FindAll(".ds-glossary-abilities").Count > 0)
            .Should().Be(("Lo que saben los jugadores", true));
    }

    [Fact]
    public void A_vault_creature_note_has_the_section_too()
    {
        var cut = RenderDetail("vault:Monstruos/Lobo.md");

        cut.Find(".glossary-detail .journal-creature .journal-thumb__label").TextContent.Should().Be("Lyra · Lobo");
    }

    [Fact]
    public void A_creature_without_entries_has_no_section() =>
        RenderDetail("srd:criaturas/aboleth").FindAll(".journal-creature").Should().BeEmpty();

    [Fact]
    public void A_spell_never_has_the_section() =>
        RenderDetail("srd:conjuros/fireball").FindAll(".journal-creature").Should().BeEmpty();

    [Fact]
    public async Task Touching_the_thumbnail_opens_the_entry()
    {
        var cut = RenderDetail(AnkhegKey);

        await cut.InvokeAsync(() => cut.Find(".journal-creature .journal-thumb").Click());

        (Shell.ActivePanel, Shell.SelectedPcPath, Shell.SelectedJournalPath).Should().Be((OverlayPanel.Players, Lyra, Ankheg));
    }
}
