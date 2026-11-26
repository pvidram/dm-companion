using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class PlayersPanelJournalTests : JournalUiTestBase
{
    public PlayersPanelJournalTests()
    {
        Services.AddSoundForTests();
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<JournalLogService>();
    }

    private IRenderedComponent<PlayersPanel> RenderPanel()
    {
        Shell.Toggle(OverlayPanel.Players);
        return Render<PlayersPanel>();
    }

    [Fact]
    public void The_bitacora_section_is_in_the_players_panel_above_the_list_of_pcs()
    {
        var cut = RenderPanel();

        var order = cut.FindAll("section.journal-log, button.ds-list-row").Select(e => e.TagName).ToArray();

        (cut.Find("section.journal-log h3").TextContent, order[0], order.Length).Should().Be(("Bitácora", "SECTION", 3));
    }

    [Fact]
    public void The_bitacora_section_stays_when_a_pc_is_selected()
    {
        Shell.SelectPc(Lyra);

        var cut = RenderPanel();

        (cut.FindAll("section.journal-log").Count, cut.FindAll("article.character-sheet").Count).Should().Be((1, 1));
    }

    [Fact]
    public void Generar_from_the_panel_writes_the_note()
    {
        var cut = RenderPanel();

        cut.FindAll("section.journal-log button").First(b => b.TextContent.Contains("Generar bitácora común", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => cut.Find("section.journal-log .ds-notice").TextContent.Trim().Should().Be("Bitácora generada: 0 entradas"), Wait);
    }
}
