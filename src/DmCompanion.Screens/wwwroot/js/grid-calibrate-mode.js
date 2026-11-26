// Modo «Calibrar con un rectángulo» del mapa: traduce los punteros a un rectángulo y de él a la cuadrícula. Sin DOM ni
// Konva (los puntos llegan en px de cliente y se convierten con `env.toMap`), para probarlo con `node --test` (tests/js).
// Un dedo traza el rectángulo; un segundo dedo lo cancela (es zoom) y no se vuelve a trazar hasta soltarlos todos.
// El lápiz traza en exclusiva: mientras está apoyado los toques (la palma) se ignoran.
(function (root) {
    'use strict';

    function create(env) {
        const { calibrateGrid } = root.gridCalibrate || require('./grid-calibrate.js');
        const fingers = new Set();
        let tracer = null; // pointerId que traza, o null
        let pen = null; // pointerId del lápiz apoyado, o null
        let anchor = null; // esquina de partida en px de mapa
        let draft = null;
        let committed = null;
        let blocked = false; // hubo un segundo dedo: no se traza hasta que no quede ninguno
        let columns = 1;
        let rows = 1;

        const normalized = (a, b) => ({
            x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), width: Math.abs(a.x - b.x), height: Math.abs(a.y - b.y),
        });
        const computed = () => (committed ? calibrateGrid(committed, columns, rows) : null);

        function cancelDraft() {
            const had = draft !== null;
            tracer = null;
            anchor = null;
            draft = null;
            if (had) env.onChange();
        }

        return {
            zooms: true,
            grid: () => computed() ?? { ...env.grid },
            rect: () => (committed ? { ...committed } : null),
            draft: () => (draft ? { ...draft } : null),
            cellSize: () => computed()?.cellSize ?? null,
            setCells(c, r) {
                if (!Number.isInteger(c) || c < 1 || !Number.isInteger(r) || r < 1) return;
                columns = c;
                rows = r;
                env.onChange();
            },
            down(id, x, y, type) {
                if (type === 'pen') {
                    pen = id;
                    blocked = false;
                    draft = null;
                    tracer = id;
                    anchor = env.toMap({ x, y });
                    return;
                }
                if (pen !== null) return;
                fingers.add(id);
                if (fingers.size > 1) {
                    blocked = true;
                    cancelDraft();
                } else if (!blocked) {
                    tracer = id;
                    anchor = env.toMap({ x, y });
                }
            },
            move(id, x, y) {
                if (id !== tracer || !anchor) return;
                draft = normalized(anchor, env.toMap({ x, y }));
                env.onChange();
            },
            up(id) {
                if (id === pen) {
                    pen = null;
                    if (draft && draft.width >= 1 && draft.height >= 1) committed = draft;
                    cancelDraft();
                    return;
                }
                if (!fingers.delete(id)) return;
                if (id === tracer) {
                    if (draft && draft.width >= 1 && draft.height >= 1) committed = draft;
                    cancelDraft();
                }
                if (fingers.size === 0) blocked = false;
            },
            tracks: id => id === pen || fingers.has(id),
            // Sin dedos en pantalla no puede quedar ninguno apuntado (un pointerup perdido dejaría un fantasma).
            reset() {
                fingers.clear();
                pen = null;
                blocked = false;
                cancelDraft();
            },
        };
    }

    const api = { create };
    root.gridCalibrateMode = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
