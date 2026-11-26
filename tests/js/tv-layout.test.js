const test = require('node:test');
const assert = require('node:assert');
const { stageTransform, fitFrameToViewport, visibleStripEntries, hpClass, availableViewport, pickContentScale, sheetView, slideView } = require('../../src/DmCompanion.Screens/wwwroot/js/tv/tv-layout.js');

test('encuadre 16:9 en pantalla 16:9 escala 1.2 sin márgenes', () => {
    const t = stageTransform({ x: 0, y: 0, width: 1600, height: 900 }, { width: 1920, height: 1080 });
    assert.deepStrictEqual([t.scale, t.x, t.y], [1.2, 0, 0]);
});

test('encuadre cuadrado deja márgenes laterales', () => {
    const t = stageTransform({ x: 0, y: 0, width: 1000, height: 1000 }, { width: 1920, height: 1080 });
    assert.deepStrictEqual([t.scale, t.x, t.y], [1.08, 420, 0]);
});

test('el origen del encuadre se desplaza al origen de la pantalla', () => {
    const t = stageTransform({ x: 100, y: 50, width: 1600, height: 900 }, { width: 1920, height: 1080 });
    assert.deepStrictEqual([t.x, t.y], [-120, -60]);
});

test('encuadre vacío no rompe', () => {
    const t = stageTransform({ x: 0, y: 0, width: 0, height: 0 }, { width: 1920, height: 1080 });
    assert.deepStrictEqual([t.scale, t.x, t.y], [1, 0, 0]);
});

test('visibleStripEntries conserva el orden y descarta entradas inválidas', () => {
    const entries = [{ name: 'A' }, null, { name: 'B' }];
    assert.deepStrictEqual(visibleStripEntries(entries).map(e => e.name), ['A', 'B']);
});

test('visibleStripEntries sin lista devuelve vacío', () => {
    assert.deepStrictEqual(visibleStripEntries(null), []);
});

test('hpClass', () => {
    assert.deepStrictEqual(
        [hpClass('Ileso'), hpClass('Herido'), hpClass('Malherido'), hpClass('12/20'), hpClass(null), hpClass('')],
        ['ileso', 'herido', 'malherido', 'exacto', null, null]);
});

test('availableViewport descuenta la tira y marca dónde empieza el mapa', () => {
    assert.deepStrictEqual(availableViewport({ width: 1920, height: 1080 }, 120), { width: 1920, height: 960, top: 120 });
});

test('availableViewport sin tira usa toda la pantalla', () => {
    assert.deepStrictEqual(availableViewport({ width: 1920, height: 1080 }, 0), { width: 1920, height: 1080, top: 0 });
});

test('availableViewport nunca da alto cero', () => {
    assert.strictEqual(availableViewport({ width: 1920, height: 1080 }, 5000).height, 1);
});

test('pickContentScale elige la primera escala que cabe', () => {
    assert.deepStrictEqual(pickContentScale(s => s <= 0.8), { scale: 0.8, overflow: false });
});

test('pickContentScale a escala 1 si cabe de entrada', () => {
    assert.deepStrictEqual(pickContentScale(() => true), { scale: 1, overflow: false });
});

test('pickContentScale se queda en 0.6 y avisa si no cabe', () => {
    assert.deepStrictEqual(pickContentScale(() => false), { scale: 0.6, overflow: true });
});

test('sheetView sin ficha devuelve null', () => {
    assert.strictEqual(sheetView(undefined), null);
});

test('sheetView conserva retrato y estadísticas de texto', () => {
    assert.deepStrictEqual(sheetView({ portraitUrl: 'files/3', stats: [{ label: 'CA', value: '14' }] }),
        { portraitUrl: 'files/3', stats: [{ label: 'CA', value: '14' }] });
});

test('sheetView descarta retratos ajenos y estadísticas mal formadas', () => {
    assert.deepStrictEqual(sheetView({ portraitUrl: 'http://otro/x.png', stats: [{ label: 'CA', value: 14 }, null, { label: 'PG', value: '27' }] }),
        { portraitUrl: null, stats: [{ label: 'PG', value: '27' }] });
});

test('slideView conserva imagen propia, texto y pie', () => {
    assert.deepStrictEqual(slideView({ kind: 'slide', image: 'files/3', text: 'Nota', caption: 'Lyra · Ankheg' }),
        { image: 'files/3', text: 'Nota', caption: 'Lyra · Ankheg' });
});

test('slideView sin imagen ni texto deja solo el pie', () => {
    assert.deepStrictEqual(slideView({ image: null, text: null, caption: 'Lyra · Ankheg' }), { image: null, text: null, caption: 'Lyra · Ankheg' });
});

test('slideView descarta imágenes ajenas y textos vacíos', () => {
    assert.deepStrictEqual(slideView({ image: 'http://otro/x.png', text: '', caption: 7 }), { image: null, text: null, caption: '' });
});

test('slideView de algo que no es un objeto no rompe', () => {
    assert.deepStrictEqual(slideView(undefined), { image: null, text: null, caption: '' });
});

test('fitFrameToViewport: encuadre 16:9 con escenario más bajo crece en ancho', () => {
    const f = fitFrameToViewport({ x: 100, y: 50, width: 1600, height: 900 }, { width: 1920, height: 980 });
    assert.ok(Math.abs(f.width / f.height - 1920 / 980) < 1e-9);
    assert.deepStrictEqual([f.height, f.y], [900, 50]);
    assert.ok(Math.abs(f.x + f.width / 2 - 900) < 1e-9);
});

test('fitFrameToViewport: escenario más alto crece en alto alrededor del centro', () => {
    const f = fitFrameToViewport({ x: 0, y: 100, width: 1000, height: 500 }, { width: 1000, height: 1000 });
    assert.deepStrictEqual([f.x, f.y, f.width, f.height], [0, -150, 1000, 1000]);
});

test('fitFrameToViewport: misma proporción no cambia el encuadre', () => {
    const f = fitFrameToViewport({ x: 10, y: 20, width: 1600, height: 900 }, { width: 1920, height: 1080 });
    assert.deepStrictEqual(f, { x: 10, y: 20, width: 1600, height: 900 });
});

test('fitFrameToViewport: nunca es menor que el encuadre original', () => {
    const o = { x: 5, y: 5, width: 700, height: 300 };
    for (const v of [{ width: 1920, height: 980 }, { width: 800, height: 1200 }, { width: 100, height: 100 }]) {
        const f = fitFrameToViewport(o, v);
        assert.ok(f.width >= o.width - 1e-9 && f.height >= o.height - 1e-9);
        assert.ok(f.x <= o.x + 1e-9 && f.y <= o.y + 1e-9);
    }
});

test('fitFrameToViewport: encuadre vacío se devuelve tal cual', () => {
    const o = { x: 0, y: 0, width: 0, height: 0 };
    assert.deepStrictEqual(fitFrameToViewport(o, { width: 1920, height: 980 }), o);
});

test('fitFrameToViewport: tras ajustar, stageTransform no deja márgenes', () => {
    const v = { width: 1920, height: 980 };
    const t = stageTransform(fitFrameToViewport({ x: 100, y: 50, width: 1600, height: 900 }, v), v);
    const f = fitFrameToViewport({ x: 100, y: 50, width: 1600, height: 900 }, v);
    assert.ok(Math.abs(t.x + f.x * t.scale) < 1e-6 && Math.abs(t.y + f.y * t.scale) < 1e-6);
    assert.ok(Math.abs(f.width * t.scale - v.width) < 1e-6 && Math.abs(f.height * t.scale - v.height) < 1e-6);
});
