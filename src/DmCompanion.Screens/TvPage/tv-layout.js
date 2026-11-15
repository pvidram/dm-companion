// Funciones puras de la TV (encuadre, tira, vida): se prueban con `node --test` (tests/js). ES2017 sin módulos.
(function (root) {
    'use strict';

    // Transformación que hace caber el encuadre entero y centrado en la pantalla.
    // Un punto del mapa (mx, my) cae en (x + mx * scale, y + my * scale).
    function stageTransform(frame, viewport) {
        if (!frame || !(frame.width > 0) || !(frame.height > 0)) return { scale: 1, x: 0, y: 0 };
        var scale = Math.min(viewport.width / frame.width, viewport.height / frame.height);
        return {
            scale: scale,
            x: (viewport.width - frame.width * scale) / 2 - frame.x * scale,
            y: (viewport.height - frame.height * scale) / 2 - frame.y * scale
        };
    }

    // El servidor ya omite a los ocultos; aquí solo se descartan entradas inválidas, respetando el orden.
    function visibleStripEntries(entries) {
        var result = [];
        if (!entries) return result;
        for (var i = 0; i < entries.length; i++) {
            if (entries[i] && typeof entries[i] === 'object') result.push(entries[i]);
        }
        return result;
    }

    // Clase CSS de la vida: etiqueta («Ileso», «Herido», «Malherido»), cifra exacta de PJ («12/20») o nada.
    function hpClass(hp) {
        if (!hp) return null;
        var labels = { 'Ileso': 'ileso', 'Herido': 'herido', 'Malherido': 'malherido' };
        return labels.hasOwnProperty(hp) ? labels[hp] : 'exacto';
    }

    // Pantalla que queda libre para el mapa bajo la tira (alto de la tira = 0 sin combate). `top` es donde empieza.
    function availableViewport(viewport, stripHeight) {
        var strip = stripHeight > 0 ? stripHeight : 0;
        return { width: viewport.width, height: Math.max(1, viewport.height - strip), top: strip };
    }

    // Escalas de letra del contenido, de mayor a menor; se elige la primera cuyo texto cabe.
    var CONTENT_SCALES = [1, 0.9, 0.8, 0.7, 0.6];

    // `fits(scale)` aplica la escala y dice si el contenido cabe. Devuelve la escala elegida y si aun así desborda.
    function pickContentScale(fits) {
        for (var i = 0; i < CONTENT_SCALES.length; i++) {
            if (fits(CONTENT_SCALES[i])) return { scale: CONTENT_SCALES[i], overflow: false };
        }
        return { scale: CONTENT_SCALES[CONTENT_SCALES.length - 1], overflow: true };
    }

    // Ficha de PJ del mensaje `content`: solo pares etiqueta/valor de texto y un retrato servido por la propia app (`files/…`).
    function sheetView(sheet) {
        if (!sheet || typeof sheet !== 'object') return null;
        var stats = [];
        var list = Array.isArray(sheet.stats) ? sheet.stats : [];
        for (var i = 0; i < list.length; i++) {
            var stat = list[i];
            if (stat && typeof stat.label === 'string' && typeof stat.value === 'string') stats.push({ label: stat.label, value: stat.value });
        }
        var portrait = typeof sheet.portraitUrl === 'string' && sheet.portraitUrl.indexOf('files/') === 0 ? sheet.portraitUrl : null;
        return { portraitUrl: portrait, stats: stats };
    }

    var api = {
        stageTransform: stageTransform, visibleStripEntries: visibleStripEntries, hpClass: hpClass,
        availableViewport: availableViewport, pickContentScale: pickContentScale, CONTENT_SCALES: CONTENT_SCALES,
        sheetView: sheetView
    };
    root.tvLayout = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
