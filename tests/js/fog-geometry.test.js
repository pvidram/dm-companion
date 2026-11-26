const test = require('node:test');
const assert = require('node:assert');
const { closesAt, zoneAt, contains, simplify, closeFreehand } = require('../../src/DmCompanion.Screens/wwwroot/js/fog-geometry.js');

const square = (id, x, y, side) => ({ id, points: [x, y, x + side, y, x + side, y + side, x, y + side] });

test('cerca de la primera esquina cierra la zona', () => {
    assert.strictEqual(closesAt({ x: 0, y: 0 }, { x: 10, y: 0 }, 1, 24), true);
});

test('lejos de la primera esquina no cierra', () => {
    assert.strictEqual(closesAt({ x: 0, y: 0 }, { x: 30, y: 0 }, 1, 24), false);
});

test('con zoom alto el radio se mide en px de pantalla', () => {
    assert.strictEqual(closesAt({ x: 0, y: 0 }, { x: 10, y: 0 }, 4, 24), false);
});

test('con zoom bajo el radio abarca más mapa', () => {
    assert.strictEqual(closesAt({ x: 0, y: 0 }, { x: 80, y: 0 }, 0.25, 24), true);
});

test('en un solape zoneAt devuelve la última zona', () => {
    const zones = [square('a', 0, 0, 100), square('b', 50, 50, 100)];
    assert.strictEqual(zoneAt(zones, 75, 75).id, 'b');
});

test('fuera de todas las zonas zoneAt devuelve null', () => {
    assert.strictEqual(zoneAt([square('a', 0, 0, 100)], 200, 200), null);
});

test('el borde cuenta como dentro', () => {
    assert.strictEqual(contains(square('a', 0, 0, 100).points, 100, 50), true);
});

test('un polígono con menos de 3 esquinas no contiene nada', () => {
    assert.strictEqual(contains([0, 0, 10, 10], 5, 5), false);
});

const pt = (x, y) => ({ x, y });

test('simplify elimina los puntos alineados dentro de la tolerancia', () => {
    assert.deepStrictEqual(simplify([pt(0, 0), pt(5, 0.1), pt(10, 0)], 1), [pt(0, 0), pt(10, 0)]);
});

test('simplify conserva una esquina que supera la tolerancia', () => {
    assert.strictEqual(simplify([pt(0, 0), pt(10, 0), pt(10, 10)], 1).length, 3);
});

test('simplify con menos de 3 puntos devuelve los mismos', () => {
    assert.deepStrictEqual(simplify([pt(0, 0), pt(1, 1)], 4), [pt(0, 0), pt(1, 1)]);
});

test('closeFreehand reduce un cuadrado trazado con muchos puntos a sus cuatro esquinas', () => {
    const trace = [];
    for (let i = 0; i <= 100; i += 10) trace.push(pt(i, 0));
    for (let i = 10; i <= 100; i += 10) trace.push(pt(100, i));
    for (let i = 90; i >= 0; i -= 10) trace.push(pt(i, 100));
    for (let i = 90; i >= 1; i -= 10) trace.push(pt(0, i));
    assert.deepStrictEqual(closeFreehand(trace, 4), [0, 0, 100, 0, 100, 100, 0, 100]);
});

test('closeFreehand descarta una línea recta', () => {
    assert.strictEqual(closeFreehand([pt(0, 0), pt(50, 1), pt(100, 0)], 4), null);
});

test('closeFreehand descarta un toque sin arrastre', () => {
    assert.strictEqual(closeFreehand([pt(5, 5), pt(5, 5)], 4), null);
});

test('closeFreehand acepta un trazo que sale del mapa (coordenadas negativas)', () => {
    assert.deepStrictEqual(closeFreehand([pt(-50, -50), pt(100, -50), pt(100, 100), pt(-50, 100)], 4), [-50, -50, 100, -50, 100, 100, -50, 100]);
});

test('closeFreehand deja pasar un ocho: solo supera el filtro de JS, Core lo descarta (área con signo 0)', () => {
    const eight = [pt(0, 0), pt(100, 100), pt(100, 0), pt(0, 100)];
    assert.strictEqual(closeFreehand(eight, 4).length, 8);
});

test('un trazo que da dos vueltas alrededor del claro contiene su centro', () => {
    assert.strictEqual(contains([0, 0, 20, 0, 20, 20, 0, 20, 2, 2, 18, 2, 18, 18, 2, 18], 10, 10), true);
});

test('un ocho asimétrico contiene puntos de sus dos lóbulos', () => {
    const eight = [0, 0, 10, 10, 10, 0, 0, 6];
    assert.strictEqual(contains(eight, 1, 3), true);
    assert.strictEqual(contains(eight, 8, 5), true);
});
