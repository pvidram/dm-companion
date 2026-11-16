using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

// Modo Niebla: herramienta elegida, borrador de Trazar y avisos del lienzo (zona añadida, alternada o borrada).
public sealed partial class MapCanvasInterop
{
    /// <summary>Última herramienta elegida; se usa al volver al modo Niebla. La primera vez, Destapar.</summary>
    public FogTool FogTool { get; private set; } = FogTool.Reveal;

    /// <summary>Esquinas de la zona que se está trazando (0 sin trazado en curso).</summary>
    public int FogDraftCorners { get; private set; }

    /// <summary>Cambia el número de esquinas del trazado en curso; lo consumen «Deshacer esquina» y «Cancelar».</summary>
    public event EventHandler? FogDraftChanged;

    public Task SetFogToolAsync(FogTool tool)
    {
        FogTool = tool;
        return InvokeLoadedAsync("mapCanvas.setFogTool", tool.ToString());
    }

    public Task UndoFogCornerAsync() => InvokeLoadedAsync("mapCanvas.fogDraft", "undo");

    public Task CancelFogDraftAsync() => InvokeLoadedAsync("mapCanvas.fogDraft", "cancel");

    // Lo que viene del JS se valida en Core (AddFogZone): va al `.state.json`.
    [JSInvokable]
    public bool OnFogZoneAdded(double[] points)
    {
        if (points is null) return false;
        var accepted = false;
        session.Mutate(editor => accepted = editor.AddFogZone(points) is not null);
        return accepted;
    }

    [JSInvokable]
    public void OnFogZoneToggled(string id) => session.Mutate(editor =>
        editor.State.FogZones.FirstOrDefault(z => z.Id == id) is { } zone && editor.SetFogRevealed(id, !zone.IsRevealed));

    [JSInvokable]
    public void OnFogZoneErased(string id) => session.Mutate(editor => editor.EraseFogZone(id));

    [JSInvokable]
    public void OnFogDraftChanged(int corners) => SetFogDraftCorners(Math.Max(0, corners));

    private void SetFogDraftCorners(int corners)
    {
        if (FogDraftCorners == corners) return;
        FogDraftCorners = corners;
        FogDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    // Llamar con _gate tomado. Un lienzo nuevo nace con Destapar y sin trazado en curso.
    private async Task SendFogToolOnLoadAsync()
    {
        SetFogDraftCorners(0);
        if (FogTool != FogTool.Reveal) await TryInvokeAsync("mapCanvas.setFogTool", FogTool.ToString());
    }
}
