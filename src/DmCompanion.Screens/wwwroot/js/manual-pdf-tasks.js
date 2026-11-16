// Tareas de pdf.js del visor de manuales que no tocan el DOM: abrir sin dejar vivo el worker y pintar con un reintento.
// Puro; se prueba con `node --test` (tests/js).
(function (root) {
    'use strict';

    var RETRY_FACTOR = 0.5;

    function isCancellation(error) {
        return !!error && error.name === 'RenderingCancelledException';
    }

    // Espera al documento; si falla, destruye la tarea (y con ella el Web Worker) antes de relanzar el error.
    async function loadDocument(task) {
        try {
            return await task.promise;
        } catch (error) {
            await task.destroy().catch(function () { });
            throw error;
        }
    }

    // Pinta con renderAt(1); si falla por algo que no es una cancelación (p. ej. sin memoria), reintenta una vez con renderAt(0.5).
    // Devuelve false solo si fallan los dos intentos.
    async function renderWithRetry(renderAt) {
        for (var factor = 1; ; factor = RETRY_FACTOR) {
            try {
                await renderAt(factor);
                return true;
            } catch (error) {
                if (isCancellation(error)) return true;
                if (factor === RETRY_FACTOR) return false;
            }
        }
    }

    var api = { loadDocument: loadDocument, renderWithRetry: renderWithRetry };
    root.manualPdfTasks = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
