namespace DmCompanion.Screens.Services;

public enum OverlayPanel { None, Players, Notes, Maps }

public enum EditorDialog { None, Conflict, ConfirmClose }

public enum EditorCommand { Overwrite, Reload }

public sealed record NewNoteRequest(string Title, string? Folder);

/// <summary>Tarjeta rápida de un token. X e Y son px relativos al área del mapa, ya ajustados a sus bordes.</summary>
public sealed record TokenCardRequest(string TokenId, double X, double Y);

public enum MapNameMode { NewBlank, NewFromImport, Rename }

/// <summary>Petición del diálogo de nombre de mapa. <c>NotePath</c> solo se usa al renombrar.</summary>
public sealed record MapNameRequest(MapNameMode Mode, string? NotePath, string SuggestedName);

public sealed class ShellState
{
    public OverlayPanel ActivePanel { get; private set; }
    public string? SelectedNotePath { get; private set; }
    public string? EditingPath { get; private set; }
    public string? SelectedPcPath { get; private set; }
    public NewNoteRequest? NewNote { get; private set; }
    public bool ConflictsOpen { get; private set; }
    public bool AddTokenOpen { get; private set; }
    public TokenCardRequest? TokenCard { get; private set; }

    /// <summary>Panel flotante del historial de la tira retraída.</summary>
    public bool HistoryOpen { get; private set; }
    public bool StartCombatOpen { get; private set; }
    public string? SelectedCombatTokenId { get; private set; }
    public bool EndCombatOpen { get; private set; }

    public MapNameRequest? MapName { get; private set; }
    public string? DeletingMapPath { get; private set; }
    public bool DiscardingMap { get; private set; }

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

    public void ClosePanel(OverlayPanel panel)
    {
        if (ActivePanel != panel) return;
        ActivePanel = OverlayPanel.None;
        Notify();
    }

    public void RequestMapName(MapNameMode mode, string? notePath = null, string suggestedName = "")
    {
        MapName = new MapNameRequest(mode, notePath, suggestedName);
        Notify();
    }

    public void CloseMapName()
    {
        MapName = null;
        Notify();
    }

    public void RequestDeleteMap(string notePath)
    {
        DeletingMapPath = notePath;
        Notify();
    }

    public void CancelDeleteMap()
    {
        DeletingMapPath = null;
        Notify();
    }

    public void RequestDiscardMap()
    {
        DiscardingMap = true;
        Notify();
    }

    public void CancelDiscardMap()
    {
        DiscardingMap = false;
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

    public void OpenAddToken()
    {
        AddTokenOpen = true;
        Notify();
    }

    public void CloseAddToken()
    {
        AddTokenOpen = false;
        Notify();
    }

    public void OpenTokenCard(TokenCardRequest request)
    {
        HistoryOpen = false;
        TokenCard = request;
        Notify();
    }

    public void CloseTokenCard()
    {
        TokenCard = null;
        Notify();
    }

    public void ToggleHistory()
    {
        HistoryOpen = !HistoryOpen;
        Notify();
    }

    public void CloseHistory()
    {
        if (!HistoryOpen) return;
        HistoryOpen = false;
        Notify();
    }

    public void OpenStartCombat()
    {
        StartCombatOpen = true;
        Notify();
    }

    public void CloseStartCombat()
    {
        StartCombatOpen = false;
        Notify();
    }

    public void SelectCombatToken(string? tokenId)
    {
        SelectedCombatTokenId = tokenId;
        Notify();
    }

    public void RequestEndCombat()
    {
        EndCombatOpen = true;
        Notify();
    }

    public void CancelEndCombat()
    {
        EndCombatOpen = false;
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
        AddTokenOpen = false;
        TokenCard = null;
        HistoryOpen = false;
        StartCombatOpen = false;
        SelectedCombatTokenId = null;
        EndCombatOpen = false;

        MapName = null;
        DeletingMapPath = null;
        DiscardingMap = false;
        EditorDirty = false;
        _pendingNavigation = null;
        ActiveEditorDialog = EditorDialog.None;
        Notify();
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
