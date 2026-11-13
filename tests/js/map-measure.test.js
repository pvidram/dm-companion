const test = require('node:test');
const assert = require('node:assert');
const { measureText } = require('../../src/DmCompanion.Screens/wwwroot/js/map-measure.js');

const grid = { enabled: true, cellSize: 70, offsetX: 0, offsetY: 0 };

test('mide el caso del plan: de (35, 35) a (245, 105) son 15 pies', () => {
    assert.strictEqual(measureText(grid, 5, 'pies', { x: 35, y: 35 }, { x: 245, y: 105 }), '15 pies');
});

test('una diagonal cuenta como una casilla (Chebyshev)', () => {
    assert.strictEqual(measureText(grid, 5, 'pies', { x: 35, y: 35 }, { x: 105, y: 105 }), '5 pies');
});

test('el desplazamiento de la cuadrícula cambia las casillas', () => {
    const shifted = { ...grid, offsetX: 40 };
    assert.strictEqual(measureText(shifted, 5, 'pies', { x: 35, y: 35 }, { x: 45, y: 35 }), '5 pies');
});

test('usa la unidad y la cantidad que recibe', () => {
    assert.strictEqual(measureText(grid, 1.5, 'm', { x: 35, y: 35 }, { x: 105, y: 35 }), '1.5 m');
});

test('sin cuadrícula muestra píxeles', () => {
    assert.strictEqual(measureText({ ...grid, enabled: false }, 5, 'pies', { x: 0, y: 0 }, { x: 30, y: 40 }), '50 px');
});
