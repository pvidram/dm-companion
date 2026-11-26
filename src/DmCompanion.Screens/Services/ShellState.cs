using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

public enum OverlayPanel { None, Players, Notes, Maps, Sound, Vault, Tv, Glossary }

public enum GlossaryTab { Glossary, Manuals }

/// <summary>Lateral abierto del visor de manuales; solo hay uno a la vez.</summary>
public enum ManualPane { None, Outline, Search, Bookmarks }

/// <summary>Pestaña de un PJ en Jugadores.</summary>
public enum PlayerTab { Sheet, Journal }

public enum EditorDialog { None, Conflict, ConfirmClose }

public enum EditorCommand { Overwrite, Reload }

public sealed record NewNoteRequest(string Title, string? Folder);

public enum JournalFormMode { New, Edit, Shared }

/// <param name="PcPath">PJ propuesto en un formulario nuevo; null = el último usado o el primero.</param>
/// <param name="EntryPath">Entrada que se edita (solo con Edit).</param>
public sealed record JournalFormRequest(JournalFormMode Mode, string? PcPath = null, string? EntryPath = null);

public enum MapNameMode { NewBlank, NewFromImport, Rename }

/// <summary>Petición del diálogo de nombre de mapa. <c>NotePath</c> solo se usa al renombrar.</summary>
public sealed record MapNameRequest(MapNameMode Mode, string? NotePath, string SuggestedName);

public enum SceneDialogMode { Save, Rename }

/// <summary>Petición del diálogo de nombre de escena. <c>SceneName</c> solo se usa al renombrar.</summary>
public sealed record SceneDialogRequest(SceneDialogMode Mode, string? SceneName);

public sealed class ShellState
{
    public OverlayPanel ActivePanel { get; private set; }
    public string? SelectedNotePath { get; private set; }
    public string? EditingPath { get; private set; }
    public string? SelectedPcPath { get; private set; }

    public PlayerTab PlayerTab { get; private set; }

    /// <summary>Entrada abierta en la pestaña Diario del PJ; null = la lista.</summary>
    public string? SelectedJournalPath { get; private set; }

    /// <summary>Último PJ al que se le guardó una entrada en esta sesión; el formulario lo propone.</summary>
    public string? LastJournalPcPath { get; private set; }

    /// <summary>Formulario «Nueva entrada» / «Editar entrada» abierto; su estado vive aquí porque se pinta en la ranura Dialogs de DmShell.</summary>
    public JournalFormRequest? JournalForm { get; private set; }

    /// <summary>«Atrás» o Escape sobre un formulario con cambios sin guardar: espera «Descartar cambios» o «Seguir editando».</summary>
    public bool ConfirmingJournalDiscard { get; private set; }

    private Func<bool>? _journalFormDirty;

    /// <summary>Hay cambios sin guardar en el formulario abierto (los publica él mismo con <see cref="SetJournalFormDirtyProbe"/>).</summary>
    public bool IsJournalFormDirty => _journalFormDirty?.Invoke() == true;

    /// <summary>Entrada que espera la confirmación de borrado.</summary>
    public string? DeletingJournalPath { get; private set; }

    /// <summary>Aviso sobre la pestaña Diario (p. ej. «No se pudo leer 2 imagen(es)»); lo cierra el DM.</summary>
    public string? JournalNotice { get; private set; }
    public string? SelectedGlossaryKey { get; private set; }
    public GlossaryTab GlossaryTab { get; private set; }
    public string? OpenManualPath { get; private set; }
    public ManualPane ActiveManualPane { get; private set; }
    /// <summary>«Ampliar»: el visor de manuales ocupa casi toda la pantalla (spec 5b §6). Vuelve a false al cerrar el manual.</summary>
    public bool ManualViewerExpanded { get; private set; }
    public NewNoteRequest? NewNote { get; private set; }
    public bool ConflictsOpen { get; private set; }
    public bool AddTokenOpen { get; private set; }

    /// <summary>Panel flotante del historial de la tira retraída.</summary>
    public bool HistoryOpen { get; private set; }
    public bool StartCombatOpen { get; private set; }

    /// <summary>Panel rápido de sonido (favoritos y escenas), anclado bajo el mini control de la franja.</summary>
    public bool SoundQuickOpen { get; private set; }
    public string? SelectedCombatTokenId { get; private set; }
    public bool EndCombatOpen { get; private set; }

    public MapNameRequest? MapName { get; private set; }
    public string? DeletingMapPath { get; private set; }
    public SceneDialogRequest? SceneDialog { get; private set; }
    public string? DeletingScene { get; private set; }
    public string? OverwritingScene { get; private set; }
    public bool DiscardingMap { get; private set; }

    /// <summary>Carpeta que no parece un vault y espera confirmación para abrirse desde el panel «Cambiar vault».</summary>
    public string? ConfirmingVaultPath { get; private set; }

    /// <summary>Por qué no se pudo cambiar de vault; se muestra en el panel, que vuelve a abrirse.</summary>
    public string? VaultSwitchError { get; private set; }

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
        VaultSwitchError = null;
        Notify();
    }

    public void RequestVaultConfirm(string path)
    {
        ConfirmingVaultPath = path;
        Notify();
    }

    public void CloseVaultConfirm()
    {
        if (ConfirmingVaultPath is null) return;
        ConfirmingVaultPath = null;
        Notify();
    }

    public void ShowVaultSwitchError(string error)
    {
        ActivePanel = OverlayPanel.Vault;
        VaultSwitchError = error;
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
        if (SelectedPcPath != relativePath) SelectedJournalPath = null;
        SelectedPcPath = relativePath;
        Notify();
    }

    public void SelectPlayerTab(PlayerTab tab)
    {
        PlayerTab = tab;
        if (tab != PlayerTab.Journal) SelectedJournalPath = null;
        Notify();
    }

    public void OpenJournalEntry(string entryPath, string pcPath)
    {
        ActivePanel = OverlayPanel.Players;
        SelectedPcPath = pcPath;
        PlayerTab = PlayerTab.Journal;
        SelectedJournalPath = entryPath;
        Notify();
    }

    public void CloseJournalEntry()
    {
        if (SelectedJournalPath is null) return;
        SelectedJournalPath = null;
        Notify();
    }

    public void RememberJournalPc(string pcPath) => LastJournalPcPath = pcPath;

    public void OpenJournalForm(JournalFormRequest request)
    {
        JournalForm = request;
        Notify();
    }

    public void SetJournalFormDirtyProbe(Func<bool>? probe) => _journalFormDirty = probe;

    /// <summary>El formulario está guardando: atrás y Escape no lo cierran (cerrar a mitad borraría los temporales que el almacén lee).</summary>
    public bool IsJournalFormSaving { get; private set; }

    public void SetJournalFormSaving(bool saving) => IsJournalFormSaving = saving;

    public void CloseJournalForm()
    {
        _journalFormDirty = null;
        IsJournalFormSaving = false;
        ConfirmingJournalDiscard = false;
        if (JournalForm is null) return;
        JournalForm = null;
        Notify();
    }

    /// <summary>Cierre «accidental» (atrás, Escape): con cambios sin guardar pide confirmación en vez de cerrar.</summary>
    public void RequestCloseJournalForm()
    {
        if (IsJournalFormSaving) return;
        if (!IsJournalFormDirty)
        {
            CloseJournalForm();
            return;
        }
        ConfirmingJournalDiscard = true;
        Notify();
    }

    public void CancelJournalDiscard()
    {
        if (!ConfirmingJournalDiscard) return;
        ConfirmingJournalDiscard = false;
        Notify();
    }

    public void RequestDeleteJournalEntry(string entryPath)
    {
        DeletingJournalPath = entryPath;
        Notify();
    }

    public void CancelDeleteJournalEntry()
    {
        DeletingJournalPath = null;
        Notify();
    }

    public void SetJournalNotice(string? text)
    {
        JournalNotice = text;
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

    public void RequestSaveScene() => SetSceneDialog(new SceneDialogRequest(SceneDialogMode.Save, null));

    public void RequestRenameScene(string sceneName) => SetSceneDialog(new SceneDialogRequest(SceneDialogMode.Rename, sceneName));

    public void CloseSceneDialog() => SetSceneDialog(null);

    public void RequestDeleteScene(string sceneName)
    {
        DeletingScene = sceneName;
        Notify();
    }

    public void RequestOverwriteScene(string sceneName)
    {
        OverwritingScene = sceneName;
        Notify();
    }

    public void CancelOverwriteScene()
    {
        OverwritingScene = null;
        Notify();
    }

    public void CancelDeleteScene()
    {
        DeletingScene = null;
        Notify();
    }

    private void SetSceneDialog(SceneDialogRequest? request)
    {
        SceneDialog = request;
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

    /// <summary>Borrador con el que se abrió el diálogo desde el glosario; null si se abrió sin él.</summary>
    public TokenDraft? AddTokenPrefill { get; private set; }

    public void OpenAddToken()
    {
        AddTokenOpen = true;
        AddTokenPrefill = null;
        Notify();
    }

    public void OpenAddToken(TokenDraft prefill)
    {
        AddTokenOpen = true;
        AddTokenPrefill = prefill;
        Notify();
    }

    public void CloseAddToken()
    {
        AddTokenOpen = false;
        AddTokenPrefill = null;
        Notify();
    }

    public void OpenGlossary(string? key = null)
    {
        ActivePanel = OverlayPanel.Glossary;
        GlossaryTab = GlossaryTab.Glossary;
        SelectedGlossaryKey = key;
        Notify();
    }

    public void SelectGlossaryTab(GlossaryTab tab)
    {
        GlossaryTab = tab;
        Notify();
    }

    public void OpenManual(string relativePath)
    {
        ActivePanel = OverlayPanel.Glossary;
        GlossaryTab = GlossaryTab.Manuals;
        OpenManualPath = relativePath;
        ActiveManualPane = ManualPane.None;
        ManualViewerExpanded = false;
        Notify();
    }

    public void CloseManual()
    {
        OpenManualPath = null;
        ActiveManualPane = ManualPane.None;
        ManualViewerExpanded = false;
        Notify();
    }

    public void ToggleManualViewerExpanded()
    {
        ManualViewerExpanded = !ManualViewerExpanded;
        Notify();
    }

    /// <summary>Abre el lateral pedido; si ya era el abierto, lo cierra.</summary>
    public void ToggleManualPane(ManualPane pane)
    {
        ActiveManualPane = ActiveManualPane == pane ? ManualPane.None : pane;
        Notify();
    }

    public void CloseManualPane()
    {
        if (ActiveManualPane == ManualPane.None) return;
        ActiveManualPane = ManualPane.None;
        Notify();
    }

    public void ShowGlossaryList()
    {
        SelectedGlossaryKey = null;
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

    public void ToggleSoundQuick()
    {
        SoundQuickOpen = !SoundQuickOpen;
        Notify();
    }

    public void CloseSoundQuick()
    {
        if (!SoundQuickOpen) return;
        SoundQuickOpen = false;
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
        PlayerTab = PlayerTab.Sheet;
        SelectedJournalPath = null;
        LastJournalPcPath = null;
        JournalForm = null;
        _journalFormDirty = null;
        ConfirmingJournalDiscard = false;
        DeletingJournalPath = null;
        JournalNotice = null;
        SelectedGlossaryKey = null;
        GlossaryTab = GlossaryTab.Glossary;
        OpenManualPath = null;
        ActiveManualPane = ManualPane.None;
        ManualViewerExpanded = false;
        NewNote = null;
        ConflictsOpen = false;
        AddTokenOpen = false;
        AddTokenPrefill = null;
        HistoryOpen = false;
        SoundQuickOpen = false;
        StartCombatOpen = false;
        SelectedCombatTokenId = null;
        EndCombatOpen = false;

        MapName = null;
        DeletingMapPath = null;
        SceneDialog = null;
        DeletingScene = null;
        OverwritingScene = null;
        ConfirmingVaultPath = null;
        VaultSwitchError = null;
        DiscardingMap = false;
        EditorDirty = false;
        _pendingNavigation = null;
        ActiveEditorDialog = EditorDialog.None;
        Notify();
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
