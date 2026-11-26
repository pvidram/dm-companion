const test = require('node:test');
const assert = require('node:assert');
const wf = require('../../src/DmCompanion.UI/wwwroot/window-frame.js');

const MIN = { width: 320, height: 240 };
const AREA = { width: 1000, height: 600 };

test('clampSize keeps a size that fits', () => {
    assert.deepStrictEqual(wf.clampSize({ width: 400, height: 300 }, MIN, AREA, 16), { width: 400, height: 300 });
});

test('clampSize raises to the minimum', () => {
    assert.deepStrictEqual(wf.clampSize({ width: 100, height: 100 }, MIN, AREA, 16), { width: 320, height: 240 });
});

test('clampSize caps to the area', () => {
    assert.deepStrictEqual(wf.clampSize({ width: 2000, height: 2000 }, MIN, AREA, 16), { width: 968, height: 568 });
});

test('clampSize lets the area win below the minimum', () => {
    assert.deepStrictEqual(wf.clampSize({ width: 400, height: 300 }, MIN, { width: 300, height: 200 }, 16), { width: 268, height: 168 });
});

test('clampPosition keeps the frame inside the margins', () => {
    const size = { width: 340, height: 300 };
    assert.deepStrictEqual(wf.clampPosition(-50, -20, size, AREA, 16), { x: 16, y: 16 });
    assert.deepStrictEqual(wf.clampPosition(900, 500, size, AREA, 16), { x: 644, y: 284 });
});

test('resizeFrom e only changes width', () => {
    assert.deepStrictEqual(wf.resizeFrom('e', { width: 300, height: 200 }, 50, 70), { width: 350, height: 200 });
});

test('resizeFrom s only changes height', () => {
    assert.deepStrictEqual(wf.resizeFrom('s', { width: 300, height: 200 }, 50, 70), { width: 300, height: 270 });
});

test('resizeFrom se changes both', () => {
    assert.deepStrictEqual(wf.resizeFrom('se', { width: 300, height: 200 }, 50, 70), { width: 350, height: 270 });
});

test('toCornerAnchor picks the bottom-right corner', () => {
    assert.deepStrictEqual(wf.toCornerAnchor(700, 400, { width: 200, height: 150 }, AREA), { corner: 'br', dx: 100, dy: 50 });
});

test('toCornerAnchor picks the top-left corner', () => {
    assert.deepStrictEqual(wf.toCornerAnchor(20, 30, { width: 200, height: 150 }, AREA), { corner: 'tl', dx: 20, dy: 30 });
});

test('fromCornerAnchor clamps into a smaller area', () => {
    const size = { width: 200, height: 150 };
    const small = { width: 600, height: 1000 };
    assert.deepStrictEqual(wf.fromCornerAnchor({ corner: 'br', dx: 100, dy: 50 }, size, small, 16), { x: 300, y: 800 });
    assert.strictEqual(wf.fromCornerAnchor({ corner: 'br', dx: 900, dy: 50 }, size, small, 16).x, 16);
});

test('isDoubleTap detects two quick still taps', () => {
    assert.strictEqual(wf.isDoubleTap({ t: 0, x: 10, y: 10, moved: false }, { t: 200, x: 12, y: 11, moved: false }), true);
});

test('isDoubleTap ignores a moved second tap', () => {
    assert.strictEqual(wf.isDoubleTap({ t: 0, x: 10, y: 10, moved: false }, { t: 200, x: 10, y: 10, moved: true }), false);
});

test('isDoubleTap ignores taps 400 ms apart', () => {
    assert.strictEqual(wf.isDoubleTap({ t: 0, x: 10, y: 10, moved: false }, { t: 400, x: 10, y: 10, moved: false }), false);
});

// --- DOM con elementos falsos ---

function fakeHandle() {
    const listeners = {};
    return {
        listeners,
        style: {},
        addEventListener(type, fn) { listeners[type] = fn; },
        removeEventListener(type, fn) { if (listeners[type] === fn) delete listeners[type]; },
        setPointerCapture() {},
        fire(type, event) {
            const e = { type, stopPropagation() { this.stopped = true; }, preventDefault() { this.prevented = true; }, target: {}, button: 0, pointerId: 1, ...event };
            if (listeners[type]) listeners[type](e);
            return e;
        },
    };
}

function fakeFrame() {
    const handles = { drag: fakeHandle(), e: fakeHandle(), s: fakeHandle(), se: fakeHandle() };
    const parent = { clientWidth: 1000, clientHeight: 600 };
    const frame = {
        style: {},
        parentElement: parent,
        handles,
        isConnected: true,
        focusCalls: 0,
        focus(options) { this.focusCalls++; this.focusOptions = options; },
        get offsetWidth() { return parseFloat(this.style.width) || 400; },
        get offsetHeight() { return parseFloat(this.style.height) || 300; },
        get offsetLeft() { return parseFloat(this.style.left) || 0; },
        get offsetTop() { return parseFloat(this.style.top) || 0; },
        querySelector: () => handles.drag,
        querySelectorAll: () => [handles.e, handles.s, handles.se].map((h, i) => Object.assign(h, { getAttribute: () => ['e', 's', 'se'][i] })),
    };
    return frame;
}

function fakeListener() {
    const calls = [];
    return { calls, invokeMethodAsync(name, arg) { calls.push({ name, arg }); return Promise.resolve(); } };
}

const OPTIONS = { movable: true, edges: 'corner', minWidth: 240, minHeight: 160, margin: 16, size: null, position: { x: 100, y: 100 }, anchor: null };

let observers;
test.beforeEach(() => {
    observers = [];
    globalThis.ResizeObserver = class { constructor(cb) { this.cb = cb; observers.push(this); } observe() {} disconnect() { this.gone = true; } };
});

test('handle pointerdown stops propagation and prevents default', () => {
    for (const key of ['drag', 'se']) {
        const frame = fakeFrame();
        wf.attach(frame, OPTIONS, fakeListener());
        const e = frame.handles[key].fire('pointerdown', { clientX: 0, clientY: 0 });
        assert.ok(e.stopped && e.prevented, key);
        assert.strictEqual(frame.handles[key].style.touchAction, 'none');
        wf.detach(frame);
    }
});

test('drag end reports the frame once', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, OPTIONS, listener);
    const h = frame.handles.drag;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    for (let i = 1; i <= 20; i++) h.fire('pointermove', { clientX: i * 5, clientY: i * 2 });
    assert.strictEqual(listener.calls.length, 0);
    h.fire('pointerup', { clientX: 100, clientY: 40 });
    assert.strictEqual(listener.calls.length, 1);
    assert.deepStrictEqual(listener.calls[0], {
        name: 'OnFrameChanged',
        arg: { x: 200, y: 140, size: null, anchor: { corner: 'tl', dx: 200, dy: 140 } },
    });
});

test('reclamp keeps the requested size', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, movable: false }, listener);
    const h = frame.handles.se;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 400, clientY: 0 });
    h.fire('pointerup', { clientX: 400, clientY: 0 });
    assert.strictEqual(frame.style.width, '800px');
    const before = listener.calls.length;
    frame.parentElement.clientWidth = 500;
    observers[0].cb();
    assert.strictEqual(frame.style.width, '468px');
    frame.parentElement.clientWidth = 1000;
    observers[0].cb();
    assert.strictEqual(frame.style.width, '800px');
    assert.strictEqual(listener.calls.length, before);
});

test('double tap on a handle resets', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, size: { width: 600, height: 400 } }, listener);
    assert.strictEqual(frame.style.width, '600px');
    const h = frame.handles.se;
    h.fire('pointerdown', { clientX: 5, clientY: 5, timeStamp: 0 });
    h.fire('pointerup', { clientX: 5, clientY: 5, timeStamp: 50 });
    h.fire('pointerdown', { clientX: 6, clientY: 5, timeStamp: 150 });
    h.fire('pointerup', { clientX: 6, clientY: 5, timeStamp: 200 });
    assert.strictEqual(frame.style.width, '');
    assert.strictEqual(frame.style.height, '');
    assert.deepStrictEqual(listener.calls.map(c => c.name), ['OnFrameReset']);
});

test('interactive targets do not start a drag', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, OPTIONS, listener);
    const h = frame.handles.drag;
    const e = h.fire('pointerdown', { clientX: 0, clientY: 0, target: { closest: () => ({}) } });
    h.fire('pointermove', { clientX: 50, clientY: 0 });
    h.fire('pointerup', { clientX: 50, clientY: 0 });
    assert.ok(!e.stopped);
    assert.strictEqual(listener.calls.length, 0);
    assert.strictEqual(frame.style.left, '100px');
});

test('update releases a half-done gesture and detach removes listeners', () => {
    const frame = fakeFrame();
    wf.attach(frame, OPTIONS, fakeListener());
    const h = frame.handles.drag;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    wf.update(frame, OPTIONS);
    h.fire('pointermove', { clientX: 50, clientY: 0 });
    assert.strictEqual(frame.style.left, '100px');
    wf.detach(frame);
    assert.deepStrictEqual(Object.keys(h.listeners), []);
    assert.ok(observers[0].gone);
});

test('moving does not report a size', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, OPTIONS, listener);
    const h = frame.handles.drag;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 60, clientY: 0 });
    h.fire('pointerup', { clientX: 60, clientY: 0 });
    assert.strictEqual(listener.calls[0].arg.size, null);
});

test('moving a frame that has a chosen size still reports no size', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, size: { width: 500, height: 350 } }, listener);
    const h = frame.handles.drag;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 60, clientY: 0 });
    h.fire('pointerup', { clientX: 60, clientY: 0 });
    assert.strictEqual(listener.calls[0].arg.size, null);
});

test('e-resize under a clamped height reports the requested width and a null height when never chosen', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, movable: false }, listener);
    const h = frame.handles.e;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 100, clientY: 0 });
    h.fire('pointerup', { clientX: 100, clientY: 0 });
    assert.deepStrictEqual(listener.calls[0].arg.size, { width: 500, height: null });
    assert.strictEqual(frame.style.height, '');
});

test('e-resize keeps the previously requested height even if it is shown clamped', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, movable: false, size: { width: null, height: 900 } }, listener);
    assert.strictEqual(frame.style.height, '568px');
    const h = frame.handles.e;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 100, clientY: 0 });
    h.fire('pointerup', { clientX: 100, clientY: 0 });
    assert.deepStrictEqual(listener.calls[0].arg.size, { width: 500, height: 900 });
});

test('s-resize keeps the requested width', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, movable: false, size: { width: 600, height: null } }, listener);
    const h = frame.handles.s;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 0, clientY: 50 });
    h.fire('pointerup', { clientX: 0, clientY: 50 });
    assert.deepStrictEqual(listener.calls[0].arg.size, { width: 600, height: 350 });
});

test('se-resize reports both axes', () => {
    const frame = fakeFrame();
    const listener = fakeListener();
    wf.attach(frame, { ...OPTIONS, movable: false }, listener);
    const h = frame.handles.se;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 30, clientY: 40 });
    h.fire('pointerup', { clientX: 30, clientY: 40 });
    assert.deepStrictEqual(listener.calls[0].arg.size, { width: 430, height: 340 });
});

test('an axis without a chosen size has no inline style', () => {
    const frame = fakeFrame();
    wf.attach(frame, { ...OPTIONS, size: { width: 500, height: null } }, fakeListener());
    assert.strictEqual(frame.style.width, '500px');
    assert.strictEqual(frame.style.height, '');
    wf.update(frame, { ...OPTIONS, size: { width: null, height: 300 } });
    assert.strictEqual(frame.style.width, '');
    assert.strictEqual(frame.style.height, '300px');
});

test('detach clears the inline size', () => {
    const frame = fakeFrame();
    wf.attach(frame, { ...OPTIONS, size: { width: 700, height: 500 } }, fakeListener());
    assert.strictEqual(frame.style.width, '700px');
    wf.detach(frame);
    assert.strictEqual(frame.style.width, '');
    assert.strictEqual(frame.style.height, '');
});

test('pointerdown on a handle focuses the frame so it comes to front', () => {
    for (const key of ['drag', 'se']) {
        const frame = fakeFrame();
        wf.attach(frame, OPTIONS, fakeListener());
        frame.handles[key].fire('pointerdown', { clientX: 0, clientY: 0 });
        assert.strictEqual(frame.focusCalls, 1, key);
        assert.deepStrictEqual(frame.focusOptions, { preventScroll: true });
        wf.detach(frame);
    }
});

test('the observer detaches a frame that left the DOM', () => {
    const frame = fakeFrame();
    wf.attach(frame, OPTIONS, fakeListener());
    frame.isConnected = false;
    observers[0].cb();
    assert.ok(observers[0].gone);
});

test('a rejected callback promise does not escape', async () => {
    const frame = fakeFrame();
    const listener = { invokeMethodAsync: () => Promise.reject(new Error('released')) };
    wf.attach(frame, OPTIONS, listener);
    const h = frame.handles.drag;
    h.fire('pointerdown', { clientX: 0, clientY: 0 });
    h.fire('pointermove', { clientX: 50, clientY: 0 });
    h.fire('pointerup', { clientX: 50, clientY: 0 });
    await new Promise(resolve => setImmediate(resolve));
});
