// Cámara del lienzo del mapa (map-canvas.js): zoom manteniendo un punto fijo y encuadre del mapa entero.
(function (root) {
    'use strict';

    const { mapSize } = root.mapCore;

    const MAX_ZOOM = 8;

    function clampScale(inst, scale) {
        const min = inst.fitScale > 0 ? inst.fitScale * 0.5 : 0.05;
        return Math.min(MAX_ZOOM, Math.max(min, scale));
    }

    // Zoom manteniendo fijo el punto `center` (coordenadas del contenedor).
    function zoomAt(inst, center, newScale) {
        const stage = inst.stage;
        const scale = clampScale(inst, newScale);
        const old = stage.scaleX();
        const world = { x: (center.x - stage.x()) / old, y: (center.y - stage.y()) / old };
        stage.scale({ x: scale, y: scale });
        stage.position({ x: center.x - world.x * scale, y: center.y - world.y * scale });
    }

    function fitView(inst) {
        const { w, h } = mapSize(inst);
        const vw = inst.stage.width();
        const vh = inst.stage.height();
        if (!(w > 0 && h > 0 && vw > 0 && vh > 0)) return false;
        const scale = Math.min(vw / w, vh / h);
        inst.fitScale = scale;
        inst.stage.scale({ x: scale, y: scale });
        inst.stage.position({ x: (vw - w * scale) / 2, y: (vh - h * scale) / 2 });
        return true;
    }

    function tryFit(inst) {
        if (inst.needsFit && fitView(inst)) inst.needsFit = false;
    }

    root.mapView = { zoomAt, tryFit };
})(window);
