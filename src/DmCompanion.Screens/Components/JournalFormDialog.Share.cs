using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Components;

// «Compartir» de Android: lo recibido espera en IncomingShare hasta que el formulario lo recoge.
public sealed partial class JournalFormDialog
{
    private readonly List<SharedFile> _incoming = [];
    private int _incomingUnreadable;
    private IncomingShare? _share;

    private void InitializeShare()
    {
        _share = Services.GetService<IncomingShare>();
        if (_share is not null) _share.Received += OnShareReceived;
    }

    private void OnShareReceived(object? sender, EventArgs e) => _ = TakeShareOnDispatcherAsync();

    private async Task TakeShareOnDispatcherAsync()
    {
        try
        {
            await InvokeAsync(() =>
            {
                TakeShare();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // El formulario ya se retiró: lo recibido queda en IncomingShare para el siguiente.
        }
        catch (Exception ex)
        {
            Log.LogError("Fallo al recoger lo compartido ({ExceptionType}).", ex.GetType().FullName);
        }
    }

    // Con la edición abierta lo compartido espera; cerrada o en «Nueva entrada», se recoge (decisión 36).
    private void TakeShare()
    {
        // Guardando no se recoge nada: Finish borra los temporales del borrador y se llevaría lo recién llegado.
        if (_busy || _share is not { HasPending: true } || Shell.JournalForm is { Mode: JournalFormMode.Edit }) return;
        var batch = _share.Take();
        _incoming.AddRange(batch.Files);
        _incomingUnreadable += batch.Unreadable;
        if (Shell.JournalForm is null) Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.Shared));
        else DrainIncoming();
    }

    private void DrainIncoming()
    {
        foreach (var file in _incoming) _draft.AddShared(file);
        _unreadable += _incomingUnreadable;
        _incoming.Clear();
        _incomingUnreadable = 0;
    }

    private Stream OpenShared(SharedFile file) => _share!.Open(file);

    private void DiscardSharedFiles() => _share?.Discard(_draft.AllSharedFiles);

    private void DisposeShare()
    {
        if (_share is not null) _share.Received -= OnShareReceived;
        DiscardSharedFiles();
    }
}
