using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class ShellStateTests
{
    [Fact]
    public void RequestNewNote_stores_the_request_and_raises_Changed()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.RequestNewNote("Mazmorra", "Lugares");

        (shell.NewNote, raised).Should().Be((new NewNoteRequest("Mazmorra", "Lugares"), 1));
    }

    [Fact]
    public void CloseNewNote_clears_the_request_and_raises_Changed()
    {
        var shell = new ShellState();
        shell.RequestNewNote("Mazmorra", null);
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.CloseNewNote();

        (shell.NewNote, raised).Should().Be((null, 1));
    }

    [Fact]
    public void Reset_clears_panel_selections_and_dialogs()
    {
        var shell = new ShellState();
        shell.EditNote("Personajes/Lyra.md");
        shell.SelectPc("Personajes/Torvald.md");
        shell.RequestNewNote("x", null);
        shell.OpenConflicts();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.Reset();

        (shell.ActivePanel, shell.SelectedNotePath, shell.EditingPath, shell.SelectedPcPath, shell.NewNote, shell.ConflictsOpen, raised)
            .Should().Be((OverlayPanel.None, null, null, null, null, false, 1));
    }
}
