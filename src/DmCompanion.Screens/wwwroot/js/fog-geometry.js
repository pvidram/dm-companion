// Geometría de las zonas de niebla en la tablet: piezas puras, para probarlas con `node --test` (tests/js).
// Misma regla que FogGeometry de Core (vueltas/nonzero): el borde cuenta como dentro y, en un solape, manda la última zona creada.
// También simplifica y cierra el trazo a mano alzada (Douglas-Peucker); la validez final del polígono la decide Core.
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

    // Regla de vueltas (nonzero), la misma con la que Konva rellena la zona: un trazo que rodea un claro dos veces lo contiene.
    // `points` es la lista plana x0, y0, x1, y1…
    function contains(points, x, y) {
        const n = Math.floor((points || []).length / 2);
        if (n < 3) return false;
        let winding = 0;
        for (let i = 0, j = n - 1; i < n; j = i++) {
            const xi = points[2 * i], yi = points[2 * i + 1], xj = points[2 * j], yj = points[2 * j + 1];
            if (onSegment(xi, yi, xj, yj, x, y)) return true;
            const side = (xi - xj) * (y - yj) - (x - xj) * (yi - yj);
            if (yj <= y && yi > y && side > 0) winding++;
            else if (yj > y && yi <= y && side < 0) winding--;
        }
        return winding !== 0;
    }

    // La última zona de la lista que contiene el punto, o null.
    function zoneAt(zones, x, y) {
        const list = zones || [];
        for (let i = list.length - 1; i >= 0; i--) if (contains(list[i].points, x, y)) return list[i];
        return null;
    }

    function distanceToLine(p, a, b) {
        const dx = b.x - a.x, dy = b.y - a.y;
        const length = Math.hypot(dx, dy);
        if (length < EPSILON) return Math.hypot(p.x - a.x, p.y - a.y);
        return Math.abs(dx * (a.y - p.y) - (a.x - p.x) * dy) / length;
    }

    // Douglas-Peucker sobre la polilínea abierta (iterativo, con pila). Conserva siempre el primero y el último.
    function simplify(points, tolerance) {
        if (!points || points.length < 3) return (points || []).slice();
        const keep = new Array(points.length).fill(false);
        keep[0] = keep[points.length - 1] = true;
        const stack = [[0, points.length - 1]];
        while (stack.length > 0) {
            const [from, to] = stack.pop();
            let worst = -1, worstDistance = tolerance;
            for (let i = from + 1; i < to; i++) {
                const d = distanceToLine(points[i], points[from], points[to]);
                if (d > worstDistance) { worst = i; worstDistance = d; }
            }
            if (worst < 0) continue;
            keep[worst] = true;
            stack.push([from, worst], [worst, to]);
        }
        return points.filter((_, i) => keep[i]);
    }

    // Lista plana x0, y0, x1, y1… del polígono del trazo, o null si no sirve (menos de 3 vértices o sin área).
    function closeFreehand(points, tolerance) {
        let trace = (points || []).slice();
        if (trace.length > 1) {
            const first = trace[0], last = trace[trace.length - 1];
            if (Math.hypot(last.x - first.x, last.y - first.y) <= tolerance) trace.pop();
        }
        // Se simplifica el anillo cerrado (volviendo al primer punto): en la polilínea abierta, la esquina previa al
        // cierre quedaría casi alineada con la recta último-primero y se perdería.
        if (trace.length > 0) trace = simplify([...trace, trace[0]], tolerance).slice(0, -1);
        if (trace.length < 3) return null;
        // Suma de los triángulos del abanico desde el primer vértice, en valor absoluto: un ocho no es
        // degenerado y pasa este filtro de JS; un ocho perfectamente simétrico tiene área con signo 0 y
        // FogGeometry.IsValid de Core lo descarta sin avisar.
        let fan = 0;
        for (let i = 1; i < trace.length - 1; i++) {
            fan += Math.abs((trace[i].x - trace[0].x) * (trace[i + 1].y - trace[0].y)
                - (trace[i + 1].x - trace[0].x) * (trace[i].y - trace[0].y)) / 2;
        }
        if (fan <= 1e-6) return null;
        return trace.flatMap(p => [p.x, p.y]);
    }

    const api = { closesAt, zoneAt, contains, simplify, closeFreehand };
    root.fogGeometry = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
