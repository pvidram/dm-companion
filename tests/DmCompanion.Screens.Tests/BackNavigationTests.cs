using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class BackNavigationTests
{
    private readonly ShellState _shell = new();
    private BackNavigation Back => new(_shell);

    [Fact]
    public async Task Dirty_editor_opens_ConfirmClose_and_is_handled()
    {
        _shell.EditNote("Lugares/Puerto Sal.md");
        _shell.SetEditorDirty(true);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ActiveEditorDialog, _shell.EditingPath).Should().Be((true, EditorDialog.ConfirmClose, "Lugares/Puerto Sal.md"));
    }

    [Fact]
    public async Task Back_with_ConfirmClose_already_open_dismisses_it()
    {
        _shell.EditNote("a.md");
        _shell.SetEditorDirty(true);
        await Back.HandleBackAsync();

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ActiveEditorDialog, _shell.EditingPath).Should().Be((true, EditorDialog.None, "a.md"));
    }

    [Fact]
    public async Task Dirty_editor_with_Conflict_dialog_closes_only_the_dialog()
    {
        _shell.EditNote("a.md");
        _shell.SetEditorDirty(true);
        _shell.ShowEditorDialog(EditorDialog.Conflict);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ActiveEditorDialog, _shell.EditorDirty, _shell.EditingPath).Should().Be((true, EditorDialog.None, true, "a.md"));
    }

    [Fact]
    public async Task Editor_dialog_open_is_closed()
    {
        _shell.EditNote("a.md");
        _shell.ShowEditorDialog(EditorDialog.Conflict);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ActiveEditorDialog, _shell.EditingPath).Should().Be((true, EditorDialog.None, "a.md"));
    }

    [Fact]
    public async Task New_note_dialog_is_closed()
    {
        _shell.Toggle(OverlayPanel.Notes);
        _shell.RequestNewNote("X", null);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.NewNote, _shell.ActivePanel).Should().Be((true, null, OverlayPanel.Notes));
    }

    [Fact]
    public async Task Conflicts_dialog_is_closed()
    {
        _shell.OpenConflicts();

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ConflictsOpen).Should().Be((true, false));
    }

    [Fact]
    public async Task Open_editor_without_changes_is_closed()
    {
        _shell.EditNote("a.md");

        var handled = await Back.HandleBackAsync();

        (handled, _shell.EditingPath, _shell.ActivePanel).Should().Be((true, null, OverlayPanel.Notes));
    }

    [Fact]
    public async Task Open_panel_is_closed()
    {
        _shell.Toggle(OverlayPanel.Players);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ActivePanel).Should().Be((true, OverlayPanel.None));
    }

    [Fact]
    public async Task Nothing_open_is_not_handled()
    {
        var handled = await Back.HandleBackAsync();

        handled.Should().BeFalse();
    }
}
