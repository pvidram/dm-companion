const test = require('node:test');
const assert = require('node:assert');
const { clampPanelPosition } = require('../../src/DmCompanion.Screens/wwwroot/js/drag-panel.js');

const area = { width: 1000, height: 600 };
const panel = { width: 340, height: 300 };

test('dentro del área no cambia la posición', () => {
    assert.deepStrictEqual(clampPanelPosition(200, 100, panel, area, 16), { x: 200, y: 100 });
});

test('no sale por la izquierda ni por arriba: gana el margen', () => {
    assert.deepStrictEqual(clampPanelPosition(-50, -20, panel, area, 16), { x: 16, y: 16 });
});

test('no sale por la derecha ni por abajo', () => {
    assert.deepStrictEqual(clampPanelPosition(900, 500, panel, area, 16), { x: 1000 - 340 - 16, y: 600 - 300 - 16 });
});

test('si el área es más pequeña que el panel gana el margen izquierdo y superior', () => {
    assert.deepStrictEqual(clampPanelPosition(50, 50, panel, { width: 200, height: 100 }, 16), { x: 16, y: 16 });
});

function fakeHandle() {
    const listeners = {};
    return {
        listeners,
        addEventListener: (type, fn) => { listeners[type] = fn; },
        setPointerCapture() { if (this.fail) throw new Error('InvalidStateError'); },
        fire(type, event) { listeners[type]({ stopPropagation() {}, preventDefault() {}, target: {}, button: 0, ...event }); },
    };
}

function fakePanel() {
    return { offsetLeft: 100, offsetTop: 100, offsetWidth: 340, offsetHeight: 300, offsetParent: { clientWidth: 1000, clientHeight: 600 }, style: {} };
}

test('un arrastre interrumpido (lostpointercapture) no deja el asa muerta', () => {
    const { bind } = require('../../src/DmCompanion.Screens/wwwroot/js/drag-panel.js');
    const panel = fakePanel();
    const handle = fakeHandle();
    bind(panel, handle);
    handle.fire('pointerdown', { pointerId: 1, clientX: 0, clientY: 0 });
    handle.fire('lostpointercapture', { pointerId: 1 });
    handle.fire('pointerdown', { pointerId: 2, clientX: 0, clientY: 0 });
    handle.fire('pointermove', { pointerId: 2, clientX: 50, clientY: 0 });
    assert.strictEqual(panel.style.left, '150px');
});

test('reset suelta un arrastre a medias', () => {
    const { bind } = require('../../src/DmCompanion.Screens/wwwroot/js/drag-panel.js');
    const panel = fakePanel();
    const handle = fakeHandle();
    const binding = bind(panel, handle);
    handle.fire('pointerdown', { pointerId: 1, clientX: 0, clientY: 0 });
    binding.reset();
    handle.fire('pointerdown', { pointerId: 2, clientX: 0, clientY: 0 });
    handle.fire('pointermove', { pointerId: 2, clientX: 20, clientY: 0 });
    assert.strictEqual(panel.style.left, '120px');
});

test('si setPointerCapture falla no empieza el arrastre', () => {
    const { bind } = require('../../src/DmCompanion.Screens/wwwroot/js/drag-panel.js');
    const panel = fakePanel();
    const handle = fakeHandle();
    handle.fail = true;
    bind(panel, handle);
    handle.fire('pointerdown', { pointerId: 1, clientX: 0, clientY: 0 });
    handle.fire('pointermove', { pointerId: 1, clientX: 50, clientY: 0 });
    assert.strictEqual(panel.style.left, undefined);
});

test('el arrastre se acota al área', () => {
    const { bind } = require('../../src/DmCompanion.Screens/wwwroot/js/drag-panel.js');
    const panel = fakePanel();
    const handle = fakeHandle();
    bind(panel, handle);
    handle.fire('pointerdown', { pointerId: 1, clientX: 0, clientY: 0 });
    handle.fire('pointermove', { pointerId: 1, clientX: 5000, clientY: 0 });
    assert.strictEqual(panel.style.left, (1000 - 340 - 16) + 'px');
});
