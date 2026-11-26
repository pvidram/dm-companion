const test = require('node:test');
const assert = require('node:assert');

globalThis.window = globalThis.window || {};
require('../../src/DmCompanion.Screens/wwwroot/js/map-mode.js');
const { touchBlocked } = window.mapMode;

const inst = (adjust, lastPenAt) => ({ adjust, mode: 'Navigate', dto: { readOnly: false }, gesture: null, lastPenAt });

test('touchBlocked is true while calibrating with a recent pen', () => {
    assert.strictEqual(touchBlocked(inst({ zooms: true }, performance.now())), true);
});

test('touchBlocked is false while calibrating without a recent pen', () => {
    assert.strictEqual(touchBlocked(inst({ zooms: true }, -Infinity)), false);
});

test('touchBlocked is false in the finger grid adjust even with a recent pen', () => {
    assert.strictEqual(touchBlocked(inst({ zooms: false }, performance.now())), false);
});
