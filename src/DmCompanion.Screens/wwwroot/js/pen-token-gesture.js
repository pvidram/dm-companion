// Lápiz sobre un token en modo Dibujar: clasificación pura (sin DOM ni Konva) para probarla con `node --test` (tests/js).
(function (root) {
    'use strict';

    const PEN_HOLD_MS = 400;
    const PEN_SLOP_PX = 8;

    // 'pending' (aún no se sabe) | 'tap' (toque corto) | 'grab' (coger el token) | 'stroke' (pintar).
    function classifyPenGesture(elapsedMs, distancePx, released) {
        if (distancePx > PEN_SLOP_PX) return 'stroke';
        if (elapsedMs >= PEN_HOLD_MS) return 'grab';
        return released ? 'tap' : 'pending';
    }

    // Token bajo un punto del mapa. `circles` va de abajo arriba (orden de dibujo): gana el último que lo contiene.
    function tokenAt(point, circles) {
        for (let i = circles.length - 1; i >= 0; i--) {
            const c = circles[i];
            if (Math.hypot(point.x - c.x, point.y - c.y) <= c.radius) return c.id;
        }
        return null;
    }

    // Al vencer el temporizador de 400 ms el tiempo ya está cumplido (no se vuelve a medir: relojes y redondeo
    // podrían dejar el gesto esperando para siempre): solo importa si el lápiz se ha movido.
    function penTimerAction(phase, distancePx) {
        if (phase !== 'pending') return 'none';
        return distancePx > PEN_SLOP_PX ? 'stroke' : 'grab';
    }

    // Un movimiento mientras se espera: 'stroke' si ya se alejó demasiado antes de tiempo, si no 'none'.
    function penMoveAction(phase, elapsedMs, distancePx) {
        if (phase !== 'pending') return 'none';
        return classifyPenGesture(elapsedMs, distancePx, false) === 'stroke' ? 'stroke' : 'none';
    }

    // Al levantar el lápiz: 'commit' (guardar el movimiento del token cogido), 'tap' (abrir la tarjeta) o 'none'.
    function penReleaseAction(phase, elapsedMs, distancePx) {
        if (phase === 'grab') return 'commit';
        if (phase !== 'pending') return 'none';
        return classifyPenGesture(elapsedMs, distancePx, true) === 'tap' ? 'tap' : 'none';
    }

    // Posición del token cogido: la de partida más lo que se ha movido el lápiz en el mapa.
    function grabPosition(home, startWorld, pointWorld) {
        return { x: home.x + pointWorld.x - startWorld.x, y: home.y + pointWorld.y - startWorld.y };
    }

    // Soltar sin haber movido el token cogido no guarda nada (evita una escritura y un viaje por Syncthing).
    function hasMoved(home, position) {
        return position.x !== home.x || position.y !== home.y;
    }

    // Puntos (con presión) que llegan mientras el gesto está pendiente. Devuelve una lista nueva; solo guarda lo necesario.
    function bufferPenPoints(buffer, points) {
        return buffer.concat(points.map(p => ({ clientX: p.clientX, clientY: p.clientY, pressure: p.pressure, type: p.type })));
    }

    // Puntos con los que empieza el trazo: el de apoyo y todos los acumulados. Si no es trazo, se descartan (toque o coger).
    function pendingStrokePoints(action, start, buffer) {
        return action === 'stroke' ? [start].concat(buffer) : [];
    }

    const api = { bufferPenPoints, pendingStrokePoints, classifyPenGesture, hasMoved, tokenAt, penTimerAction, penMoveAction, penReleaseAction, grabPosition, PEN_HOLD_MS, PEN_SLOP_PX };
    root.penTokenGesture = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
