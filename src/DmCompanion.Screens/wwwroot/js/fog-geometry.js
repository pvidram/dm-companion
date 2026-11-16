// Geometría de las zonas de niebla en la tablet: piezas puras, para probarlas con `node --test` (tests/js).
// Misma regla que FogGeometry de Core: el borde cuenta como dentro y, en un solape, manda la última zona creada.
(function (root) {
    'use strict';

    const EPSILON = 1e-9;

    // ¿El toque `point` cierra el polígono? El radio va en px de pantalla: `scale` es la escala del escenario.
    function closesAt(first, point, scale, radiusPx) {
        if (!first || !point || !(scale > 0)) return false;
        return Math.hypot(point.x - first.x, point.y - first.y) * scale <= radiusPx;
    }

    function onSegment(ax, ay, bx, by, px, py) {
        const dx = bx - ax;
        const dy = by - ay;
        const lengthSq = dx * dx + dy * dy;
        const t = lengthSq < EPSILON ? 0 : Math.min(1, Math.max(0, ((px - ax) * dx + (py - ay) * dy) / lengthSq));
        return Math.hypot(ax + t * dx - px, ay + t * dy - py) <= EPSILON;
    }

    // `points` es la lista plana x0, y0, x1, y1…
    function contains(points, x, y) {
        const n = Math.floor((points || []).length / 2);
        if (n < 3) return false;
        let inside = false;
        for (let i = 0, j = n - 1; i < n; j = i++) {
            const xi = points[2 * i], yi = points[2 * i + 1], xj = points[2 * j], yj = points[2 * j + 1];
            if (onSegment(xi, yi, xj, yj, x, y)) return true;
            if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    // La última zona de la lista que contiene el punto, o null.
    function zoneAt(zones, x, y) {
        const list = zones || [];
        for (let i = list.length - 1; i >= 0; i--) if (contains(list[i].points, x, y)) return list[i];
        return null;
    }

    const api = { closesAt, zoneAt, contains };
    root.fogGeometry = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
