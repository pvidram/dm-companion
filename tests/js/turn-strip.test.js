const test = require('node:test');
const assert = require('node:assert');
const { dropIndex } = require('../../src/DmCompanion.Screens/wwwroot/js/turn-strip.js');

const mids = [50, 150, 250];

test('soltar sobre el centro de la propia fila no la mueve', () => {
    assert.strictEqual(dropIndex(mids, 1, 150), 1);
});

test('soltar por encima de todas la deja la primera', () => {
    assert.strictEqual(dropIndex(mids, 2, 0), 0);
});

test('soltar por debajo de todas la deja la última', () => {
    assert.strictEqual(dropIndex(mids, 0, 400), 2);
});
