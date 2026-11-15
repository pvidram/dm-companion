using DmCompanion.Core.Tv;

namespace DmCompanion.Screens.Services;

// Encuadre de la TV en el lienzo de la tablet: la zona visible (para fijarlo) y el rectángulo que lo marca.
public sealed partial class MapCanvasInterop
{
    // Encuadre pedido y el que tiene pintado el lienzo cargado: un lienzo nuevo lo recibe al cargar.
    private TvFrame? _tvFrame;
    private TvFrame? _drawnTvFrame;

    /// <summary>Zona del mapa que se ve en la tablet, en coordenadas del mapa; null sin lienzo cargado o sin respuesta válida.</summary>
    public async Task<TvFrame?> VisibleRectAsync() =>
        await QueryLoadedAsync("mapCanvas.viewRect", 4) is [var x, var y, var width and > 0, var height and > 0]
            ? new TvFrame(x, y, width, height)
            : null;

    /// <summary>Rectángulo del encuadre de la TV sobre el mapa, o null para quitarlo. Se recuerda para el próximo lienzo.</summary>
    public async Task SetTvFrameAsync(TvFrame? frame)
    {
        await _gate.WaitAsync();
        try
        {
            _tvFrame = frame;
            await DrawTvFrameLockedAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Llamar con _gate tomado.
    private async Task DrawTvFrameLockedAsync()
    {
        if (!_loaded || _tvFrame == _drawnTvFrame) return;
        if (await TryInvokeAsync("mapCanvas.setTvFrame", _tvFrame)) _drawnTvFrame = _tvFrame;
    }
}
