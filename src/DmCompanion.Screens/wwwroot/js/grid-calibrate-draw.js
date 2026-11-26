// Pinta en la capa de borrador el rectángulo de «Calibrar con un rectángulo» (en curso o confirmado).
(function (root) {
    'use strict';

    // Rectángulo en px de mapa con el grosor constante en pantalla; los colores salen del tema como la medición.
    function draw(inst) {
        const rect = inst.adjust.draft() ?? inst.adjust.rect();
        if (!rect) {
            clear(inst);
            return;
        }
        if (!inst.calibrateRect || !inst.calibrateRect.getLayer()) { // clearDraft de los gestos puede haberlo destruido
            const colors = root.mapCore.theme(inst.host);
            inst.calibrateRect = new Konva.Rect({
                stroke: colors.measureLight, strokeWidth: colors.measureLightWidth, strokeScaleEnabled: false,
                fill: 'rgba(0, 0, 0, 0.12)', listening: false,
            });
            inst.layers.draft.add(inst.calibrateRect);
        }
        inst.calibrateRect.setAttrs(rect);
        inst.layers.draft.batchDraw();
    }

    function clear(inst) {
        if (!inst.calibrateRect) return;
        inst.calibrateRect.destroy();
        inst.calibrateRect = null;
        inst.layers.draft.batchDraw();
    }

    root.gridCalibrateDraw = { draw, clear };
})(typeof window !== 'undefined' ? window : globalThis);
