using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class ShellStateJournalTests
{
    private const string Lyra = "Personajes/Lyra.md";
    private const string Torvald = "Personajes/Torvald.md";
    private const string Entry = "Personajes/Lyra/Diario/2026-11-16 Ankheg.md";

    [Fact]
    public void OpenJournalEntry_opens_the_players_panel_on_the_pc_diary_with_the_entry()
    {
        var shell = new ShellState();

        shell.OpenJournalEntry(Entry, Lyra);

        (shell.ActivePanel, shell.SelectedPcPath, shell.PlayerTab, shell.SelectedJournalPath)
            .Should().Be((OverlayPanel.Players, Lyra, PlayerTab.Journal, Entry));
    }

    [Fact]
    public void OpenJournalEntry_replaces_whatever_panel_was_open_and_raises_Changed_once()
    {
        var shell = new ShellState();
        shell.OpenGlossary("srd:criaturas/ankheg");
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.OpenJournalEntry(Entry, Lyra);

        (shell.ActivePanel, raised).Should().Be((OverlayPanel.Players, 1));
    }

    [Fact]
    public void CloseJournalEntry_goes_back_to_the_list_of_the_tab()
    {
        var shell = new ShellState();
        shell.OpenJournalEntry(Entry, Lyra);

        shell.CloseJournalEntry();

        (shell.SelectedJournalPath, shell.PlayerTab).Should().Be((null, PlayerTab.Journal));
    }

    [Fact]
    public void CloseJournalEntry_with_nothing_open_raises_nothing()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.CloseJournalEntry();

        raised.Should().Be(0);
    }

    [Fact]
    public void Selecting_another_pc_closes_the_open_entry_but_keeps_the_tab()
    {
        var shell = new ShellState();
        shell.OpenJournalEntry(Entry, Lyra);

        shell.SelectPc(Torvald);

        (shell.SelectedJournalPath, shell.PlayerTab).Should().Be((null, PlayerTab.Journal));
    }

    [Fact]
    public void Selecting_the_same_pc_keeps_the_open_entry()
    {
        var shell = new ShellState();
        shell.OpenJournalEntry(Entry, Lyra);

        shell.SelectPc(Lyra);

        shell.SelectedJournalPath.Should().Be(Entry);
    }

    [Fact]
    public void Going_back_to_the_sheet_closes_the_open_entry()
    {
        var shell = new ShellState();
        shell.OpenJournalEntry(Entry, Lyra);

        shell.SelectPlayerTab(PlayerTab.Sheet);

        (shell.PlayerTab, shell.SelectedJournalPath).Should().Be((PlayerTab.Sheet, null));
    }

    [Fact]
    public void RememberJournalPc_keeps_the_last_pc_without_raising_Changed()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.RememberJournalPc(Torvald);

        (shell.LastJournalPcPath, raised).Should().Be((Torvald, 0));
    }

    [Fact]
    public void Reset_forgets_the_diary_navigation_and_the_last_pc()
    {
        var shell = new ShellState();
        shell.OpenJournalEntry(Entry, Lyra);
        shell.RememberJournalPc(Torvald);

        shell.Reset();

        (shell.PlayerTab, shell.SelectedJournalPath, shell.LastJournalPcPath).Should().Be((PlayerTab.Sheet, null, null));
    }
}
