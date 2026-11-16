// Página visible del visor de manuales: libera (cleanup) las páginas de pdf.js que se dejan de usar,
// para que hojear o buscar no acumule bitmaps hasta el destroy() final. Puro; se prueba con `node --test` (tests/js).
(function (root) {
    'use strict';

    function createPageTracker() {
        var visible = null;
        return {
            // Nueva página visible: la anterior, si es otra, se libera.
            show: function (page) {
                var previous = visible;
                visible = page;
                if (previous && previous !== page) previous.cleanup();
            },
            // Página usada de paso (búsqueda, PNG para la TV): se libera salvo que sea la visible.
            release: function (page) {
                if (page && page !== visible) page.cleanup();
            }
        };
    }

    var api = { createPageTracker: createPageTracker };
    root.manualPageTracker = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
