const test = require('node:test');
const assert = require('node:assert');
const base = '../../src/DmCompanion.Screens/wwwroot/js/';
require(base + 'fog-geometry.js');
const { fillFor, addFreehandPoint, freehandGesture, onPenDown, setTool } = require(base + 'fog-mode.js');

const palette = { fog: 'F', npc: 'N' };
const pt = (x, y) => ({ x, y });

function fakeInst(zones = []) {
    const calls = [];
    return {
        calls, fogTool: 'Freehand', dto: { fog: zones },
        stage: { scaleX: () => 1 },
        dotnet: { invokeMethodAsync: (m, ...a) => { calls.push([m, ...a]); return Promise.resolve(); } },
        layers: { draft: { destroyChildren() { calls.push(['clear']); }, batchDraw() {}, add() {} } },
    };
}

test('fillFor usa el velo de PNJ solo para las zonas de PNJ', () => {
    assert.deepStrictEqual([fillFor({ type: 'npc' }, palette), fillFor({ type: 'total' }, palette), fillFor({}, palette)], ['N', 'F', 'F']);
});

test('addFreehandPoint ignora un punto a menos de 2 px de pantalla del anterior', () => {
    const gesture = { pts: [pt(0, 0)] };
    addFreehandPoint(gesture, pt(1, 0), 1);
    assert.strictEqual(gesture.pts.length, 1);
});

test('addFreehandPoint no recorta a los límites del mapa', () => {
    const gesture = { pts: [pt(0, 0)] };
    addFreehandPoint(gesture, pt(-500, 900), 1);
    assert.deepStrictEqual(gesture.pts[1], pt(-500, 900));
});

test('al soltar, el trazo se cierra y se entrega a C# como zona', () => {
    const inst = fakeInst();
    freehandGesture.end(inst, { pts: [pt(0, 0), pt(100, 0), pt(100, 100), pt(0, 100)] });
    assert.deepStrictEqual(inst.calls.filter(c => c[0] === 'OnFogZoneAdded'), [['OnFogZoneAdded', [0, 0, 100, 0, 100, 100, 0, 100]]]);
});

test('al soltar un trazo recto no se avisa a C#', () => {
    const inst = fakeInst();
    freehandGesture.end(inst, { pts: [pt(0, 0), pt(50, 1), pt(100, 0)] });
    assert.strictEqual(inst.calls.filter(c => c[0] === 'OnFogZoneAdded').length, 0);
});

test('al soltar se limpia siempre el borrador', () => {
    const inst = fakeInst();
    freehandGesture.end(inst, { pts: [pt(0, 0)] });
    assert.strictEqual(inst.calls.filter(c => c[0] === 'clear').length, 1);
});

test('cancelar limpia el borrador sin avisar a C#', () => {
    const inst = fakeInst();
    freehandGesture.cancel(inst, { pts: [pt(0, 0), pt(100, 0), pt(100, 100)] });
    assert.deepStrictEqual(inst.calls, [['clear']]);
});

test('Cambiar tipo avisa de la última zona que contiene el punto, aunque esté destapada', () => {
    const inst = fakeInst([
        { id: 'a', points: [0, 0, 100, 0, 100, 100, 0, 100], revealed: false },
        { id: 'b', points: [0, 0, 100, 0, 100, 100, 0, 100], revealed: true },
    ]);
    inst.fogTool = 'Retype';
    onPenDown(inst, pt(50, 50));
    assert.deepStrictEqual(inst.calls, [['OnFogZoneRetyped', 'b']]);
});

test('Mano alzada no hace nada en onPenDown (lo lleva el gesto)', () => {
    const inst = fakeInst([{ id: 'a', points: [0, 0, 100, 0, 100, 100, 0, 100] }]);
    onPenDown(inst, pt(50, 50));
    assert.deepStrictEqual(inst.calls, []);
});

test('cambiar de herramienta a mitad de un trazo a mano alzada cancela el gesto', () => {
    const inst = fakeInst();
    inst.gesture = { kind: 'fogFreehand' };
    const cancelled = [];
    setTool(inst, 'Trace', i => cancelled.push(i));
    assert.deepStrictEqual([cancelled.length, inst.fogTool], [1, 'Trace']);
});

test('cambiar de herramienta no cancela otros gestos', () => {
    const inst = fakeInst();
    inst.gesture = { kind: 'stroke' };
    const cancelled = [];
    setTool(inst, 'Erase', i => cancelled.push(i));
    assert.deepStrictEqual([cancelled.length, inst.fogTool], [0, 'Erase']);
});
