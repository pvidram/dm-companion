const test = require('node:test');
const assert = require('node:assert');
const { tvFrameRect, viewRect } = require('../../src/DmCompanion.Screens/wwwroot/js/tv-frame.js');

const stage = (x, y, width, height, scale) => ({
    x: () => x, y: () => y, width: () => width, height: () => height, scaleX: () => scale,
});

test('sin encuadre no hay rectángulo', () => {
    assert.strictEqual(tvFrameRect(null, '#C9A24A'), null);
});

test('un encuadre sin área no se pinta', () => {
    assert.strictEqual(tvFrameRect({ x: 0, y: 0, width: 0, height: 900 }, '#C9A24A'), null);
});

test('el rectángulo cubre el encuadre en coordenadas del mapa', () => {
    const rect = tvFrameRect({ x: -350, y: 0, width: 1600, height: 900 }, '#C9A24A');
    assert.deepStrictEqual([rect.x, rect.y, rect.width, rect.height, rect.stroke], [-350, 0, 1600, 900, '#C9A24A']);
});

test('es fino y discontinuo a cualquier zoom y no capta eventos', () => {
    const rect = tvFrameRect({ x: 0, y: 0, width: 10, height: 10 }, '#C9A24A');
    assert.deepStrictEqual([rect.strokeScaleEnabled, rect.listening, rect.dash.length > 0, rect.strokeWidth <= 2], [false, false, true, true]);
});

test('la zona visible pasa de pantalla a coordenadas del mapa', () => {
    assert.deepStrictEqual(viewRect(stage(-200, -100, 1000, 600, 2)), [100, 50, 500, 300]);
});

test('sin escala válida no hay zona visible', () => {
    assert.strictEqual(viewRect(stage(0, 0, 1000, 600, 0)), null);
});
