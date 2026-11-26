const test = require('node:test');
const assert = require('node:assert');
const { calibrateGrid } = require('../../src/DmCompanion.Screens/wwwroot/js/grid-calibrate.js');

test('calibrates a regular rectangle', () => {
    assert.deepStrictEqual(calibrateGrid({ x: 130, y: 55, width: 300, height: 200 }, 3, 2),
        { enabled: true, cellSize: 100, offsetX: 30, offsetY: 55 });
});

test('averages an irregular rectangle', () => {
    assert.strictEqual(calibrateGrid({ x: 0, y: 0, width: 300, height: 220 }, 3, 2).cellSize, 105);
});

test('a rectangle drawn backwards gives the same grid', () => {
    assert.deepStrictEqual(calibrateGrid({ x: 430, y: 255, width: -300, height: -200 }, 3, 2),
        calibrateGrid({ x: 130, y: 55, width: 300, height: 200 }, 3, 2));
});

test('the offset is wrapped to a non-negative value', () => {
    const grid = calibrateGrid({ x: -30, y: -30, width: 300, height: 300 }, 3, 3);
    assert.strictEqual(grid.offsetX, 70);
    assert.strictEqual(grid.offsetY, 70);
});

test('rejects invalid cell counts', () => {
    const rect = { x: 0, y: 0, width: 100, height: 100 };
    for (const [c, r] of [[0, 1], [1, 0], [-2, 1], [1.5, 1], [NaN, 1], [1, undefined]]) {
        assert.strictEqual(calibrateGrid(rect, c, r), null, `${c},${r}`);
    }
});

test('rejects a degenerate or non-finite rectangle', () => {
    assert.strictEqual(calibrateGrid({ x: 0, y: 0, width: 0, height: 10 }, 1, 1), null);
    assert.strictEqual(calibrateGrid({ x: 0, y: 0, width: 10, height: 0 }, 1, 1), null);
    assert.strictEqual(calibrateGrid({ x: Infinity, y: 0, width: 10, height: 10 }, 1, 1), null);
});
