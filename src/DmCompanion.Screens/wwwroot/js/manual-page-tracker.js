// Páginas de pdf.js que el visor de manuales tiene pintadas: al sacarlas de la ventana se liberan (cleanup),
// para que desplazarse o buscar no acumule bitmaps hasta el destroy() final. Puro; se prueba con `node --test` (tests/js).
(function (root) {
    'use strict';

    function createPageTracker() {
        var visible = new Set();
        return {
            // La página entra en la ventana de pintado.
            add: function (page) {
                visible.add(page);
            },
            // La página sale de la ventana: se libera (una sola vez).
            remove: function (page) {
                if (visible.delete(page)) page.cleanup();
            },
            // Página usada de paso (búsqueda, PNG para la TV): se libera salvo que esté pintada.
            release: function (page) {
                if (page && !visible.has(page)) page.cleanup();
            }
        };
    }

    var api = { createPageTracker: createPageTracker };
    root.manualPageTracker = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
