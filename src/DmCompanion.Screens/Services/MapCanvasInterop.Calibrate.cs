using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

// Calibrar la cuadrícula con un rectángulo: estado y avisos del lienzo; el guardado es el de FinishGridAdjustAsync.
public sealed partial class MapCanvasInterop
{
    /// <summary>El ajuste en curso es una calibración (<see cref="GridAdjusting"/> también es true).</summary>
    public bool GridCalibrating { get; private set; }

    /// <summary>Último tamaño de casilla notificado por el lienzo; null mientras no hay rectángulo.</summary>
    public double? CalibrateCellSize { get; private set; }

    public Task BeginGridCalibrateAsync() => BeginAsync("mapCanvas.beginGridCalibrate", true);

    public Task SetCalibrateCellsAsync(int columns, int rows) =>
        !GridCalibrating || columns < 1 || rows < 1
            ? Task.CompletedTask
            : InvokeLoadedAsync("mapCanvas.setCalibrateCells", columns, rows);

    [JSInvokable]
    public void OnCalibrateChanged(double cellSize)
    {
        if (!GridCalibrating) return;
        CalibrateCellSize = double.IsFinite(cellSize) && cellSize > 0 ? cellSize : null;
        GridAdjustChanged?.Invoke(this, EventArgs.Empty);
    }
}
