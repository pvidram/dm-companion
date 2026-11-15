const test = require('node:test');
const assert = require('node:assert');
const { tokenBodyOpacity, DEAD_OPACITY, DOWN_OPACITY } = require('../../src/DmCompanion.Screens/wwwroot/js/token-opacity.js');

test('token sano va a opacidad completa', () => {
    assert.strictEqual(tokenBodyOpacity({ dead: false, down: false }), 1);
});

test('token a 0 HP se atenúa', () => {
    assert.strictEqual(tokenBodyOpacity({ dead: false, down: true }), DOWN_OPACITY);
});

test('token muerto se atenúa más que a 0 HP', () => {
    assert.ok(tokenBodyOpacity({ dead: true, down: false }) < DOWN_OPACITY);
});

test('muerto manda sobre 0 HP', () => {
    assert.strictEqual(tokenBodyOpacity({ dead: true, down: true }), DEAD_OPACITY);
});
