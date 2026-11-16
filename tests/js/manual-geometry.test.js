const test = require('node:test');
const assert = require('node:assert');
const { fitScale, cropToPdf, rectFromDrag, swipeDirection } = require('../../src/DmCompanion.Screens/wwwroot/js/manual-geometry.js');

test('fitScale_keeps_aspect', () => {
    const scale = fitScale(612, 792, 1000, 1000);
    assert.deepStrictEqual([Math.round(612 * scale), Math.round(792 * scale)], [773, 1000]);
});

test('fitScale_landscape_tv', () => {
    assert.strictEqual(fitScale(400, 100, 1920, 1080), 4.8);
});

test('fitScale returns 1 for an empty page', () => {
    assert.strictEqual(fitScale(0, 792, 1920, 1080), 1);
});

test('rectFromDrag_normalizes_any_direction', () => {
    assert.deepStrictEqual(rectFromDrag(300, 200, 100, 50, 400, 400), { x: 0.25, y: 0.125, width: 0.5, height: 0.375 });
});

test('rectFromDrag_clamps_to_page', () => {
    assert.deepStrictEqual(rectFromDrag(-50, 100, 500, 900, 400, 800), { x: 0, y: 0.125, width: 1, height: 0.875 });
});

test('cropToPdf_scales_fractions', () => {
    assert.deepStrictEqual(cropToPdf({ x: 0.5, y: 0.25, width: 0.25, height: 0.5 }, 600, 800), { x: 300, y: 200, width: 150, height: 400 });
});

test('swipeDirection asks for the next page on a long left swipe', () => {
    assert.strictEqual(swipeDirection(-120, 10, 60), 1);
});

test('swipeDirection ignores short or mostly vertical moves', () => {
    assert.deepStrictEqual([swipeDirection(40, 0, 60), swipeDirection(100, 80, 60)], [0, 0]);
});
