using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Components;

public sealed partial class JournalFormDialog
{
    private const long MaxImageBytes = 50L * 1024 * 1024;
    private const int MaxFilesAtOnce = 50;

    private JournalFormRequest? _seen;
    private JournalDraft _draft = new();
    private IReadOnlyList<VaultEntity> _pcs = [];
    private VaultEntity? _pc;
    private string _title = string.Empty;
    private string _note = string.Empty;
    private string? _creature;
    private string? _notice;
    private DateTime _expected;
    private (string Title, string Note, string? Creature, string Images) _loaded = (string.Empty, string.Empty, null, string.Empty);
    private int _unreadable;
    private int _missing;
    private int _dropped;
    private bool _busy;
    private IImagePicker? _picker;

    [Inject] private JournalStore Store { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;
    [Inject] private ILogger<JournalFormDialog> Log { get; set; } = default!;

    private JournalFormRequest? Request => Shell.JournalForm;

    private bool CanSave => _draft.Count > 0 && (Request?.Mode == JournalFormMode.Edit || _pc is not null);

    /// <summary>DmShell pinta el formulario y la confirmación de borrado solo si están el índice y el almacén (decisión 18).</summary>
    public static bool CanEdit(IServiceProvider services) =>
        IsAvailable(services) && services.GetService<JournalStore>() is not null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _picker = Services.GetService<IImagePicker>();
        InitializeShare();
        Sync();
        TakeShare();
    }

    // Cada petición nueva (otra instancia) reinicia el formulario; al cerrarse se borra lo que quedara sin guardar de «Compartir».
    protected override void OnVaultChanged() => Sync();

    private void Sync()
    {
        if (ReferenceEquals(_seen, Shell.JournalForm)) return;
        DiscardSharedFiles();
        _seen = Shell.JournalForm;
        if (_seen is null)
        {
            _draft = new JournalDraft();
            TakeShare();
            return;
        }
        StartDraft(_seen);
        DrainIncoming();
        Shell.SetJournalFormDirtyProbe(() => IsDirty);
    }

    private void StartDraft(JournalFormRequest request)
    {
        _draft = new JournalDraft();
        (_unreadable, _missing, _dropped, _notice) = (0, 0, 0, null);
        _pcs = Index.OfType(EntityType.Pc);
        if (request.Mode == JournalFormMode.Edit)
        {
            LoadEntry(request.EntryPath!);
            return;
        }
        (_title, _note, _creature) = (string.Empty, string.Empty, null);
        _loaded = (string.Empty, string.Empty, null, string.Empty);
        _pc = Pc(request.PcPath) ?? Pc(Shell.LastJournalPcPath) ?? _pcs.FirstOrDefault();
    }

    private VaultEntity? Pc(string? path) => path is null ? null : _pcs.FirstOrDefault(pc => pc.RelativePath == path);

    private void LoadEntry(string path)
    {
        if (Journal.Get(path) is { } entry && Journal.LastWriteOf(path) is { } stamp) Fill(entry, stamp);
        else NoEntry();
    }

    private void NoEntry()
    {
        (_title, _note, _creature, _pc) = (string.Empty, string.Empty, null, null);
        _notice = "La entrada ya no existe.";
    }

    private void Fill(JournalEntry entry, DateTime stamp)
    {
        _draft = new JournalDraft();
        (_missing, _dropped) = (0, 0);
        (_expected, _title, _note, _creature) = (stamp, entry.Title, entry.Note, entry.Creature);
        _pc = Journal.OwnerPcPathOf(entry) is { } pcPath ? Index.Get(pcPath) : null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in entry.Images)
        {
            // Un nombre no admitido o repetido haría fallar el guardado: se deja fuera del borrador, igual que las que faltan.
            if (!JournalPaths.HasSupportedExtension(image.Name) || !names.Add(image.Name)) _dropped++;
            else if (image.Exists) _draft.AddExisting(image);
            else _missing++;
        }
        _loaded = (_title, _note, _creature, ImageKey());
    }

    // Tras un cambio en disco se relee la nota del disco (el índice puede ir por detrás) y se fija la fecha que se espera.
    private async Task ReloadFromDiskAsync(string path)
    {
        if (Host.Current is not { } vault)
        {
            NoEntry();
            return;
        }
        try
        {
            var raw = await vault.ReadRawAsync(path);
            var folder = JournalPaths.FolderOf(path);
            var files = await vault.ListFilesAsync(file => JournalPaths.FolderOf(file).Equals(folder, StringComparison.OrdinalIgnoreCase));
            var present = files.Select(file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Fill(JournalReader.Read(path, raw.Content, raw.LastWriteTimeUtc, present.Contains), raw.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            NoEntry();
        }
    }

    // --- Campos ---

    private void SelectPc(VaultEntity pc) => _pc = pc;

    // --- Imágenes ---

    private async Task PickAsync()
    {
        try
        {
            foreach (var image in await _picker!.PickManyAsync())
            {
                if (JournalPaths.HasSupportedExtension(image.FileName)) _draft.AddPicked(image.FileName, image.OpenAsync);
                else _unreadable++;
            }
        }
        catch (Exception ex)
        {
            Log.LogError("No se pudo abrir el selector de imágenes ({ExceptionType}).", ex.GetType().FullName);
            _notice = "No se pudo abrir el selector de imágenes.";
        }
    }

    // InputFile invalida los IBrowserFile anteriores en cada «change»: cada fichero se lee a memoria ahora, no al guardar.
    private async Task OnFilesChosen(InputFileChangeEventArgs e)
    {
        IReadOnlyList<IBrowserFile> files;
        try
        {
            files = e.GetMultipleFiles(MaxFilesAtOnce);
        }
        catch (InvalidOperationException)
        {
            _notice = $"Demasiadas imágenes a la vez (máximo {MaxFilesAtOnce}).";
            return;
        }
        // Hasta leerlos todos, «Guardar», «Descartar», atrás y Escape esperan: si no, se guardaría solo una parte en silencio.
        var owner = !_busy;
        if (owner)
        {
            _busy = true;
            Shell.SetJournalFormSaving(true);
        }
        try
        {
            foreach (var file in files)
            {
                if (file.Size > MaxImageBytes || !JournalPaths.HasSupportedExtension(file.Name))
                {
                    _unreadable++;
                    continue;
                }
                try
                {
                    var bytes = await ReadAsync(file);
                    _draft.AddPicked(file.Name, () => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false)));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.LogWarning("No se pudo leer una imagen elegida ({ExceptionType}).", ex.GetType().FullName);
                    _unreadable++;
                }
            }
        }
        finally
        {
            if (owner)
            {
                _busy = false;
                Shell.SetJournalFormSaving(false);
                TakeShare();
            }
        }
    }

    private static async Task<byte[]> ReadAsync(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(MaxImageBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    // --- Guardar y descartar ---

    // «Descartar» es explícito y no pregunta; atrás y Escape preguntan si hay cambios sin guardar.
    private void Discard()
    {
        if (!_busy) Shell.CloseJournalForm();
    }

    private void RequestClose() => Shell.RequestCloseJournalForm();

    private string ImageKey() => string.Join("|", _draft.Images.Select(image => image.Kind == DraftImageKind.Existing ? "e:" + image.Name : "n:" + image.Id));

    // Entrada nueva: cualquier texto o imagen. Edición: cualquier diferencia con lo cargado (también reordenar o quitar).
    private bool IsDirty => (_title, _note, _creature, ImageKey()) != _loaded;

    private async Task SaveAsync()
    {
        if (_busy || Request is not { } request) return;
        _busy = true;
        Shell.SetJournalFormSaving(true);
        _notice = null;
        try
        {
            await HandleAsync(request, await WriteAsync(request));
        }
        catch (Exception ex)
        {
            Log.LogError("Fallo al guardar la entrada del diario ({ExceptionType}).", ex.GetType().FullName);
            _notice = "No se pudo guardar la entrada.";
        }
        finally
        {
            _busy = false;
            Shell.SetJournalFormSaving(false);
            TakeShare(); // lo que llegó mientras se guardaba (TakeShare no recoge con _busy)
        }
    }

    private Task<JournalResult> WriteAsync(JournalFormRequest request) => request.Mode == JournalFormMode.Edit
        ? Store.UpdateAsync(new JournalEdit(request.EntryPath!, _expected, _title, _note, _creature, _draft.ToItems(OpenShared)))
        : Store.CreateAsync(new JournalCreate(_pc!.RelativePath, _title, _note, _creature, _draft.ToSources(OpenShared)));

    private async Task HandleAsync(JournalFormRequest request, JournalResult result)
    {
        switch (result.Status)
        {
            case JournalStatus.Done:
                Finish(result);
                break;
            case JournalStatus.NoImages:
                _notice = "Añade al menos una imagen." + (result.Unreadable > 0 ? $" No se pudo leer {result.Unreadable} imagen(es)." : string.Empty);
                break;
            case JournalStatus.ModifiedOnDisk:
                // Spec §5.3: se avisa y se vuelve a cargar; no se sobrescribe lo que cambió fuera de la app.
                if (request.Mode == JournalFormMode.Edit) await ReloadFromDiskAsync(request.EntryPath!);
                _notice = "La entrada cambió fuera de la app. Se ha vuelto a cargar; revisa los cambios y guarda de nuevo.";
                break;
            case JournalStatus.NotFound:
                _notice = "La entrada ya no existe.";
                break;
            default:
                _notice = "No se pudo guardar la entrada.";
                break;
        }
    }

    // Spec decisión 17: tras guardar se abre la entrada en la pestaña Diario de su PJ y se avisa de lo descartado.
    private void Finish(JournalResult result)
    {
        var unreadable = _unreadable + result.Unreadable;
        var pcPath = _pc?.RelativePath;
        DiscardSharedFiles();
        _draft = new JournalDraft();
        if (pcPath is not null) Shell.RememberJournalPc(pcPath);
        Shell.SetJournalNotice(unreadable > 0 ? $"No se pudo leer {unreadable} imagen(es)" : null);
        // Primero se abre la entrada y después se cierra el formulario: quien vea el formulario cerrado ya ve la entrada abierta.
        if (pcPath is not null) Shell.OpenJournalEntry(result.EntryPath!, pcPath);
        Shell.CloseJournalForm();
    }

    public override void Dispose()
    {
        DisposeShare();
        base.Dispose();
    }
}
