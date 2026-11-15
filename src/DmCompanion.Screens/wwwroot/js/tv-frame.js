// Encuadre de la TV sobre el lienzo de la tablet: piezas puras, para probarlas con `node --test` (tests/js).
// map-canvas.js pinta con tvFrameRect un rectángulo fino y discontinuo que no capta eventos, y con viewRect
// informa a .NET de la zona del mapa que se ve (lo que «Fijar encuadre de TV» guarda).
(function (root) {
    'use strict';

    // En px de pantalla: con strokeScaleEnabled en false, Konva traza el borde y el discontinuo sin escalarlos con el zoom.
    const STROKE_WIDTH = 2;
    const DASH = [10, 6];

    // Configuración de Konva.Rect para el encuadre { x, y, width, height } en coordenadas del mapa; null si no hay que pintar.
    function tvFrameRect(frame, color) {
        if (!frame || !(frame.width > 0) || !(frame.height > 0)) return null;
        return {
            x: frame.x, y: frame.y, width: frame.width, height: frame.height,
            stroke: color, strokeWidth: STROKE_WIDTH, dash: DASH.slice(),
            strokeScaleEnabled: false, listening: false, perfectDrawEnabled: false,
        };
    }

    // Zona visible del escenario en coordenadas del mapa: [x, y, ancho, alto].
    function viewRect(stage) {
        const scale = stage.scaleX();
        if (!(scale > 0)) return null;
        return [-stage.x() / scale, -stage.y() / scale, stage.width() / scale, stage.height() / scale];
    }

    const api = { tvFrameRect, viewRect };
    root.tvFrame = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
