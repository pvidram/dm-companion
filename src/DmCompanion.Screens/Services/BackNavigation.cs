namespace DmCompanion.Screens.Services;

// Lógica del botón atrás de Android: respeta el texto sin guardar y cierra de lo más interno a lo más externo.
public sealed class BackNavigation(ShellState shell)
{
    // true = atrás consumido; false = no hay nada que cerrar y el host decide (enviar la app a segundo plano).
    public Task<bool> HandleBackAsync()
    {
        // El aviso «cambiado en disco» es lo más interno: atrás lo cierra y deja el texto intacto, sin pedir descartar.
        if (shell.ActiveEditorDialog == EditorDialog.Conflict)
        {
            shell.DismissEditorDialog();
            return Task.FromResult(true);
        }
        if (shell.EditorDirty)
        {
            // Con la confirmación ya abierta, atrás equivale a «Seguir editando».
            if (shell.ActiveEditorDialog == EditorDialog.ConfirmClose) shell.CancelDiscard();
            else shell.RequestCloseEditor();
            return Task.FromResult(true);
        }
        if (shell.ActiveEditorDialog != EditorDialog.None)
        {
            shell.DismissEditorDialog();
            return Task.FromResult(true);
        }
        if (shell.NewNote is not null)
        {
            shell.CloseNewNote();
            return Task.FromResult(true);
        }
        if (shell.ConflictsOpen)
        {
            shell.CloseConflicts();
            return Task.FromResult(true);
        }
        if (shell.EditingPath is not null)
        {
            shell.CloseEditor();
            return Task.FromResult(true);
        }
        if (shell.ActivePanel != OverlayPanel.None)
        {
            shell.Toggle(shell.ActivePanel);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
