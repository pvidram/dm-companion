namespace DmCompanion.Screens.Services;

public enum OverlayPanel { None, Players, Notes }

public enum EditorDialog { None, Conflict, ConfirmClose }

public enum EditorCommand { Overwrite, Reload }

public sealed record NewNoteRequest(string Title, string? Folder);

public sealed class ShellState
{
    public OverlayPanel ActivePanel { get; private set; }
    public string? SelectedNotePath { get; private set; }
    public string? EditingPath { get; private set; }
    public string? SelectedPcPath { get; private set; }
    public NewNoteRequest? NewNote { get; private set; }
    public bool ConflictsOpen { get; private set; }

    // Diálogos del editor: su estado vive aquí porque se pintan en la ranura Dialogs de DmShell.
    public EditorDialog ActiveEditorDialog { get; private set; }

    public event EventHandler? Changed;

    // El editor (en el pergamino) se suscribe; los botones de los diálogos lo invocan.
    public event Action<EditorCommand>? EditorCommandRaised;

    public void ShowEditorDialog(EditorDialog dialog)
    {
        ActiveEditorDialog = dialog;
        Notify();
    }

    // Texto sin guardar del editor; lo publica NoteEditor. Toda navegación que lo descartaría pasa por RequestNavigationAsync.
    public bool EditorDirty { get; private set; }

    private Func<Task>? _pendingNavigation;

    public void SetEditorDirty(bool dirty) => EditorDirty = dirty;

    public async Task RequestNavigationAsync(Func<Task> go)
    {
        if (!EditorDirty)
        {
            await go();
            return;
        }
        _pendingNavigation = go; // como mucho una: una petición nueva sustituye a la anterior
        ShowEditorDialog(EditorDialog.ConfirmClose);
    }

    public Task RequestOpenNote(string relativePath) => RequestNavigationAsync(() =>
    {
        OpenNote(relativePath);
        return Task.CompletedTask;
    });

    // Reeditar la nota que ya se edita no navega: no toca el texto sin guardar ni abre diálogos.
    public Task RequestEditNote(string relativePath)
    {
        if (relativePath == EditingPath) return Task.CompletedTask;
        return RequestNavigationAsync(() =>
        {
            EditNote(relativePath);
            return Task.CompletedTask;
        });
    }

    // Cerrar el editor con texto sin guardar: confirmar «Cerrar sin guardar» debe cerrarlo, no ejecutar una navegación vieja.
    public void RequestCloseEditor()
    {
        _pendingNavigation = null;
        if (EditorDirty) ShowEditorDialog(EditorDialog.ConfirmClose);
        else CloseEditor();
    }

    public async Task ConfirmDiscardAsync()
    {
        var pending = _pendingNavigation;
        _pendingNavigation = null;
        EditorDirty = false;
        ActiveEditorDialog = EditorDialog.None;
        if (pending is null) CloseEditor();
        else await pending();
    }

    public void CancelDiscard()
    {
        _pendingNavigation = null;
        DismissEditorDialog();
    }

    public void DismissEditorDialog() => ShowEditorDialog(EditorDialog.None);

    public void RaiseEditorCommand(EditorCommand command)
    {
        ActiveEditorDialog = EditorDialog.None;
        Notify();
        EditorCommandRaised?.Invoke(command);
    }

    public void Toggle(OverlayPanel panel)
    {
        ActivePanel = ActivePanel == panel ? OverlayPanel.None : panel;
        Notify();
    }

    public void OpenNote(string relativePath)
    {
        ActivePanel = OverlayPanel.Notes;
        SelectedNotePath = relativePath;
        EditingPath = null;
        EditorDirty = false;
        _pendingNavigation = null;
        ActiveEditorDialog = EditorDialog.None;
        Notify();
    }

    public void EditNote(string relativePath)
    {
        ActivePanel = OverlayPanel.Notes;
        SelectedNotePath = relativePath;
        EditingPath = relativePath;
        ActiveEditorDialog = EditorDialog.None;
        Notify();
    }

    public void CloseEditor()
    {
        EditingPath = null;
        EditorDirty = false;
        _pendingNavigation = null;
        ActiveEditorDialog = EditorDialog.None;
        Notify();
    }

    public void SelectPc(string relativePath)
    {
        SelectedPcPath = relativePath;
        Notify();
    }

    public void RequestNewNote(string? title, string? folder)
    {
        NewNote = new NewNoteRequest(title ?? string.Empty, folder);
        Notify();
    }

    public void CloseNewNote()
    {
        NewNote = null;
        Notify();
    }

    public void OpenConflicts()
    {
        ConflictsOpen = true;
        Notify();
    }

    public void CloseConflicts()
    {
        ConflictsOpen = false;
        Notify();
    }

    // Al cambiar de vault, ninguna selección ni diálogo del anterior debe sobrevivir.
    public void Reset()
    {
        ActivePanel = OverlayPanel.None;
        SelectedNotePath = null;
        EditingPath = null;
        SelectedPcPath = null;
        NewNote = null;
        ConflictsOpen = false;
        EditorDirty = false;
        _pendingNavigation = null;
        ActiveEditorDialog = EditorDialog.None;
        Notify();
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
