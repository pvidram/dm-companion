const test = require('node:test');
const assert = require('node:assert');
const { create } = require('../../src/DmCompanion.Screens/wwwroot/js/grid-calibrate-mode.js');

const start = { cellSize: 70, offsetX: 0, offsetY: 0 };

// Escala 2: un px de cliente son 0,5 px de mapa.
function mode(grid = start) {
    const changes = [];
    const m = create({ grid, toMap: p => ({ x: p.x / 2, y: p.y / 2 }), onChange: () => changes.push(1) });
    return { m, changes };
}

function draw(m, id, x1, y1, x2, y2) {
    m.down(id, x1, y1);
    m.move(id, x2, y2);
    m.up(id);
}

const first = { x: 50, y: 50, width: 150, height: 100 };

test('dragging a rectangle commits it in map pixels on release', () => {
    const { m } = mode();
    draw(m, 1, 100, 100, 400, 300);
    assert.deepStrictEqual(m.rect(), first);
    assert.strictEqual(m.draft(), null);
});

test('the rectangle is shown as a draft while dragging', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.move(1, 400, 300);
    assert.deepStrictEqual(m.draft(), first);
    assert.strictEqual(m.rect(), null);
});

test('a rectangle drawn backwards is normalized', () => {
    const { m } = mode();
    draw(m, 1, 400, 300, 100, 100);
    assert.deepStrictEqual(m.rect(), first);
});

test('grid is untouched until a rectangle exists', () => {
    const { m } = mode();
    assert.deepStrictEqual(m.grid(), start);
    assert.strictEqual(m.cellSize(), null);
});

test('the committed rectangle gives the grid with the default 1x1', () => {
    const { m } = mode();
    draw(m, 1, 100, 100, 400, 300);
    assert.strictEqual(m.cellSize(), 125);
    assert.strictEqual(m.grid().enabled, true);
});

test('setCells recalculates the grid and notifies', () => {
    const { m, changes } = mode();
    draw(m, 1, 100, 100, 400, 300);
    const before = changes.length;
    m.setCells(3, 2);
    assert.ok(Math.abs(m.cellSize() - 50) < 1e-9);
    assert.ok(changes.length > before);
});

test('setCells ignores invalid values', () => {
    const { m } = mode();
    draw(m, 1, 100, 100, 400, 300);
    for (const [c, r] of [[0, 2], [2, -1], [1.5, 1], [NaN, 1]]) m.setCells(c, r);
    assert.strictEqual(m.cellSize(), 125);
});

test('a tap without dragging keeps the previous rectangle', () => {
    const { m } = mode();
    draw(m, 1, 100, 100, 400, 300);
    m.down(1, 10, 10);
    m.up(1);
    assert.deepStrictEqual(m.rect(), first);
});

test('drawing again replaces the previous rectangle', () => {
    const { m } = mode();
    draw(m, 1, 100, 100, 400, 300);
    draw(m, 1, 20, 20, 120, 220);
    assert.deepStrictEqual(m.rect(), { x: 10, y: 10, width: 50, height: 100 });
});

test('a second finger cancels the rectangle in progress', () => {
    const { m } = mode();
    draw(m, 1, 100, 100, 400, 300);
    m.down(1, 20, 20);
    m.move(1, 200, 200);
    m.down(2, 300, 300);
    assert.strictEqual(m.draft(), null);
    assert.deepStrictEqual(m.rect(), first);
});

test('fingers lifted after a cancel do not leave a ghost', () => {
    const { m } = mode();
    m.down(1, 20, 20);
    m.move(1, 200, 200);
    m.down(2, 300, 300);
    m.up(2);
    m.up(1);
    draw(m, 3, 100, 100, 400, 300);
    assert.deepStrictEqual(m.rect(), first);
});

test('reset forgets fingers and the draft', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.move(1, 400, 300);
    m.reset();
    assert.strictEqual(m.draft(), null);
    assert.strictEqual(m.tracks(1), false);
});

test('a touch down during a pen stroke does not cancel the draft', () => {
    const { m } = mode();
    m.down(1, 100, 100, 'pen');
    m.move(1, 400, 300, 'pen');
    m.down(2, 300, 300, 'touch');
    m.move(1, 420, 320, 'pen');
    assert.deepStrictEqual(m.draft(), { x: 50, y: 50, width: 160, height: 110 });
});

test('the pen stroke commits after the palm touched and lifted', () => {
    const { m } = mode();
    m.down(1, 100, 100, 'pen');
    m.down(2, 300, 300, 'touch');
    m.move(1, 400, 300, 'pen');
    m.up(2);
    m.up(1);
    assert.deepStrictEqual(m.rect(), first);
});

test('a palm resting before the pen arrives does not stop the pen', () => {
    const { m } = mode();
    m.down(2, 300, 300, 'touch');
    m.down(3, 310, 310, 'touch');
    m.down(1, 100, 100, 'pen');
    m.move(1, 400, 300, 'pen');
    m.up(1);
    assert.deepStrictEqual(m.rect(), first);
});

test('two fingers without a pen still cancel the rectangle', () => {
    const { m } = mode();
    m.down(1, 20, 20, 'touch');
    m.move(1, 200, 200, 'touch');
    m.down(2, 300, 300, 'touch');
    assert.strictEqual(m.draft(), null);
});

test('a dropped draft notifies on reset so the preview disappears', () => {
    const { m, changes } = mode();
    m.down(1, 100, 100);
    m.move(1, 400, 300);
    const before = changes.length;
    m.reset();
    assert.strictEqual(changes.length, before + 1);
});
