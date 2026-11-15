const test = require('node:test');
const assert = require('node:assert');
const { bufferPenPoints, pendingStrokePoints, classifyPenGesture, hasMoved, tokenAt, penTimerAction, penMoveAction, penReleaseAction, grabPosition, PEN_HOLD_MS, PEN_SLOP_PX } = require('../../src/DmCompanion.Screens/wwwroot/js/pen-token-gesture.js');

test('toque corto sin moverse es tap', () => {
    assert.strictEqual(classifyPenGesture(120, 2, true), 'tap');
});

test('moverse 9 px antes de 400 ms es stroke', () => {
    assert.strictEqual(classifyPenGesture(100, 9, false), 'stroke');
});

test('400 ms quieto es grab', () => {
    assert.strictEqual(classifyPenGesture(PEN_HOLD_MS, 3, false), 'grab');
});

test('moverse 8 px exactos sigue siendo pending', () => {
    assert.strictEqual(classifyPenGesture(100, PEN_SLOP_PX, false), 'pending');
});

test('soltar tras moverse es stroke, no tap', () => {
    assert.strictEqual(classifyPenGesture(100, 9, true), 'stroke');
});

test('tokenAt devuelve el token de más arriba bajo el punto', () => {
    const circles = [
        { id: 'abajo', x: 100, y: 100, radius: 40 },
        { id: 'arriba', x: 120, y: 100, radius: 40 },
    ];
    assert.strictEqual(tokenAt({ x: 110, y: 100 }, circles), 'arriba');
});

test('tokenAt devuelve null fuera de todos los tokens', () => {
    assert.strictEqual(tokenAt({ x: 500, y: 500 }, [{ id: 'a', x: 100, y: 100, radius: 40 }]), null);
});

test('al vencer el temporizador con 8 px exactos coge el token', () => {
    assert.strictEqual(penTimerAction('pending', PEN_SLOP_PX), 'grab');
});

test('al vencer el temporizador tras moverse 9 px pinta', () => {
    assert.strictEqual(penTimerAction('pending', PEN_SLOP_PX + 1), 'stroke');
});

test('el temporizador no hace nada si el gesto ya no está pendiente', () => {
    assert.strictEqual(penTimerAction('done', 0), 'none');
});

test('moverse 9 px antes de tiempo pasa a pintar', () => {
    assert.strictEqual(penMoveAction('pending', 100, 9), 'stroke');
});

test('moverse con el token cogido no pasa a pintar', () => {
    assert.strictEqual(penMoveAction('grab', 100, 50), 'none');
});

test('levantar con el token cogido guarda el movimiento', () => {
    assert.strictEqual(penReleaseAction('grab', 900, 120), 'commit');
});

test('levantar pronto y quieto abre la tarjeta', () => {
    assert.strictEqual(penReleaseAction('pending', 150, 1), 'tap');
});

test('levantar un gesto ya terminado no hace nada', () => {
    assert.strictEqual(penReleaseAction('done', 150, 1), 'none');
});

test('grabPosition suma al origen lo que se movió el lápiz', () => {
    assert.deepStrictEqual(grabPosition({ x: 100, y: 50 }, { x: 10, y: 10 }, { x: 25, y: 4 }), { x: 115, y: 44 });
});

test('hasMoved es falso si el token sigue en su sitio', () => {
    assert.strictEqual(hasMoved({ x: 100, y: 50 }, { x: 100, y: 50 }), false);
});

test('hasMoved es verdadero si cambia cualquier coordenada', () => {
    assert.strictEqual(hasMoved({ x: 100, y: 50 }, { x: 100, y: 51 }), true);
});

test('bufferPenPoints acumula en orden conservando la presión y sin mutar la lista', () => {
    const first = bufferPenPoints([], [{ clientX: 1, clientY: 2, pressure: 0.3, type: 'pointermove' }]);
    const second = bufferPenPoints(first, [{ clientX: 3, clientY: 4, pressure: 0.6, type: 'pointermove' }]);
    assert.deepStrictEqual(second.map(p => [p.clientX, p.clientY, p.pressure]), [[1, 2, 0.3], [3, 4, 0.6]]);
    assert.strictEqual(first.length, 1);
});

test('pendingStrokePoints entrega apoyo y puntos acumulados si es trazo', () => {
    const start = { clientX: 0, clientY: 0, pressure: 0.5 };
    const buffer = [{ clientX: 2, clientY: 0, pressure: 0.4 }, { clientX: 9, clientY: 0, pressure: 0.7 }];
    assert.deepStrictEqual(pendingStrokePoints('stroke', start, buffer), [start, ...buffer]);
});

test('pendingStrokePoints descarta los puntos si es toque o coger', () => {
    const buffer = [{ clientX: 2, clientY: 0, pressure: 0.4 }];
    assert.deepStrictEqual([pendingStrokePoints('tap', {}, buffer), pendingStrokePoints('grab', {}, buffer)], [[], []]);
});
