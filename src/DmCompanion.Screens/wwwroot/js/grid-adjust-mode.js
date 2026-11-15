// Modo «Ajustando cuadrícula» del mapa: traduce los punteros a cambios de cuadrícula. Sin DOM ni Konva (los
// puntos llegan en px de cliente y se convierten con `env.toMap`), para probarlo con `node --test` (tests/js).
// Un dedo desplaza la cuadrícula; dos la pellizcan. Al cambiar el número de dedos se toma una base nueva, así no hay saltos.
(function (root) {
    'use strict';

    function create(env) {
        const { panGrid, pinchGrid } = root.gridAdjust || require('./grid-adjust.js');
        const fingers = new Map(); // pointerId -> {x, y} en px de cliente, en orden de llegada
        let grid = { ...env.grid };
        let base = null;

        const pair = () => [...fingers.values()].slice(0, 2);
        const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
        const middle = (a, b) => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });

        function rebase() {
            const points = pair();
            base = { grid: { ...grid }, count: points.length };
            if (points.length === 1) base.start = env.toMap(points[0]);
            if (points.length === 2) {
                base.distance = distance(points[0], points[1]);
                base.anchor = env.toMap(middle(points[0], points[1]));
            }
        }

        function update() {
            const points = pair();
            if (!base || points.length !== base.count || points.length === 0) return;
            if (points.length === 1) {
                const now = env.toMap(points[0]);
                grid = panGrid(base.grid, now.x - base.start.x, now.y - base.start.y);
            } else {
                const pinched = pinchGrid(base.grid, base.distance, distance(points[0], points[1]), base.anchor, env.limits);
                const now = env.toMap(middle(points[0], points[1]));
                grid = panGrid(pinched, now.x - base.anchor.x, now.y - base.anchor.y);
            }
            env.onChange(grid);
        }

        return {
            grid: () => ({ ...grid }),
            down(id, x, y) { fingers.set(id, { x, y }); rebase(); },
            move(id, x, y) {
                if (!fingers.has(id)) return;
                fingers.set(id, { x, y });
                update();
            },
            up(id) {
                if (fingers.delete(id)) rebase();
            },
            tracks: id => fingers.has(id),
            // Sin dedos en pantalla no puede quedar ninguno apuntado (un pointerup perdido dejaría un fantasma que haría pareja con el siguiente dedo).
            reset() {
                fingers.clear();
                base = null;
            },
        };
    }

    const api = { create };
    root.gridAdjustMode = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
