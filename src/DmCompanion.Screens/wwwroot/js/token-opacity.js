// Opacidad del cuerpo del token (avatar y anillos) según su estado: pura, para probarla con `node --test` (tests/js).
// Las marcas (cruz de muerto, anillo de turno, insignia de estados) no se atenúan nunca.
(function (root) {
    'use strict';

    const DEAD_OPACITY = 0.25;
    const DOWN_OPACITY = 0.4;

    function tokenBodyOpacity(token) {
        if (token.dead) return DEAD_OPACITY;
        return token.down ? DOWN_OPACITY : 1;
    }

    const api = { tokenBodyOpacity, DEAD_OPACITY, DOWN_OPACITY };
    root.tokenOpacity = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
