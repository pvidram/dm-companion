// Funciones puras del visor de manuales (escala y recorte): se prueban con `node --test` (tests/js).
(function (root) {
    'use strict';

    // Escala que hace caber una página entera en maxW × maxH sin deformarla.
    function fitScale(pageW, pageH, maxW, maxH) {
        if (!(pageW > 0) || !(pageH > 0) || !(maxW > 0) || !(maxH > 0)) return 1;
        return Math.min(maxW / pageW, maxH / pageH);
    }

    // Rectángulo en fracciones 0..1 de la página → unidades del viewport de pdf.js a escala 1 (origen arriba a la izquierda).
    function cropToPdf(rectFrac, pageW, pageH) {
        return {
            x: rectFrac.x * pageW,
            y: rectFrac.y * pageH,
            width: rectFrac.width * pageW,
            height: rectFrac.height * pageH
        };
    }

    function clamp(value, max) {
        return Math.min(Math.max(value, 0), max);
    }

    // Arrastre en píxeles de la vista (en cualquier dirección) → rectángulo en fracciones 0..1, acotado a la página.
    function rectFromDrag(x0, y0, x1, y1, viewW, viewH) {
        if (!(viewW > 0) || !(viewH > 0)) return { x: 0, y: 0, width: 0, height: 0 };
        var left = clamp(Math.min(x0, x1), viewW), right = clamp(Math.max(x0, x1), viewW);
        var top = clamp(Math.min(y0, y1), viewH), bottom = clamp(Math.max(y0, y1), viewH);
        return { x: left / viewW, y: top / viewH, width: (right - left) / viewW, height: (bottom - top) / viewH };
    }

    var api = { fitScale: fitScale, cropToPdf: cropToPdf, rectFromDrag: rectFromDrag };
    root.manualGeometry = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
