const test = require('node:test');
const assert = require('node:assert');
const { panGrid, pinchGrid } = require('../../src/DmCompanion.Screens/wwwroot/js/grid-adjust.js');

const grid = { cellSize: 70, offsetX: 10, offsetY: 20 };
const limits = { min: 20, max: 200 };
const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-9, `${actual} != ${expected}`);

test('arrastrar suma el desplazamiento y no toca el tamaño', () => {
    assert.deepStrictEqual(panGrid(grid, 5, -5), { cellSize: 70, offsetX: 15, offsetY: 15 });
});

test('arrastrar más de una casilla normaliza con módulo, también hacia atrás', () => {
    assert.deepStrictEqual(panGrid(grid, 75, -95), { cellSize: 70, offsetX: 15, offsetY: 65 });
});

test('el desplazamiento nunca llega a la casilla entera', () => {
    assert.strictEqual(panGrid(grid, 60, 0).offsetX, 0);
});

test('pellizcar escala el tamaño con la razón de distancias', () => {
    near(pinchGrid(grid, 100, 150, { x: 0, y: 0 }, limits).cellSize, 105);
});

test('pellizcar mantiene fija la línea bajo el ancla', () => {
    const anchor = { x: 333, y: 217 };
    const next = pinchGrid(grid, 100, 180, anchor, limits);
    const fraction = (value, offset, cell) => (((value - offset) / cell) % 1 + 1) % 1;
    near(fraction(anchor.x, next.offsetX, next.cellSize), fraction(anchor.x, grid.offsetX, grid.cellSize));
    near(fraction(anchor.y, next.offsetY, next.cellSize), fraction(anchor.y, grid.offsetY, grid.cellSize));
});

test('pellizcar sobre una línea la deja en el mismo sitio', () => {
    const anchor = { x: grid.offsetX + 3 * grid.cellSize, y: 0 };
    const next = pinchGrid(grid, 100, 130, anchor, limits);
    near(((anchor.x - next.offsetX) / next.cellSize) % 1, 0);
});

test('pellizcar respeta el máximo y el mínimo', () => {
    assert.strictEqual(pinchGrid(grid, 10, 1000, { x: 0, y: 0 }, limits).cellSize, 200);
    assert.strictEqual(pinchGrid(grid, 1000, 10, { x: 0, y: 0 }, limits).cellSize, 20);
});

test('con el tamaño en el límite el ancla sigue fija', () => {
    const next = pinchGrid(grid, 10, 1000, { x: 400, y: 0 }, limits);
    near(((400 - next.offsetX) / 200) % 1, ((400 - grid.offsetX) / 70) % 1);
});

test('el desplazamiento tras pellizcar queda en [0, tamaño)', () => {
    const next = pinchGrid(grid, 100, 37, { x: 900, y: 700 }, limits);
    assert.ok(next.offsetX >= 0 && next.offsetX < next.cellSize);
    assert.ok(next.offsetY >= 0 && next.offsetY < next.cellSize);
});

test('una distancia 0 no rompe: devuelve la cuadrícula sin cambios', () => {
    assert.deepStrictEqual(pinchGrid(grid, 0, 50, { x: 5, y: 5 }, limits), grid);
    assert.deepStrictEqual(pinchGrid(grid, 50, 0, { x: 5, y: 5 }, limits), grid);
    assert.deepStrictEqual(pinchGrid(grid, NaN, 50, { x: 5, y: 5 }, limits), grid);
});
