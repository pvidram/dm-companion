const test = require('node:test');
const assert = require('node:assert');
const { closesAt, zoneAt, contains } = require('../../src/DmCompanion.Screens/wwwroot/js/fog-geometry.js');

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
