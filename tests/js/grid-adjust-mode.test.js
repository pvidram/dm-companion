const test = require('node:test');
const assert = require('node:assert');
const { create } = require('../../src/DmCompanion.Screens/wwwroot/js/grid-adjust-mode.js');

const limits = { min: 20, max: 200 };
const start = { cellSize: 70, offsetX: 0, offsetY: 0 };

// Escala 2: un px de cliente son 0,5 px de mapa.
function mode(grid = start) {
    const changes = [];
    const m = create({ grid, limits, toMap: p => ({ x: p.x / 2, y: p.y / 2 }), onChange: g => changes.push(g) });
    return { m, changes };
}

test('un dedo arrastrando desplaza la cuadrícula en px de mapa', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.move(1, 120, 90);
    assert.deepStrictEqual(m.grid(), { cellSize: 70, offsetX: 10, offsetY: 65 });
});

test('dos dedos separándose agrandan la casilla', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.down(2, 200, 100);
    m.move(2, 300, 100);
    assert.strictEqual(m.grid().cellSize, 140);
});

test('al levantar un dedo el otro sigue desde donde está, sin salto', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.down(2, 200, 100);
    m.move(2, 300, 100);
    const pinched = m.grid();
    m.up(1);
    m.move(2, 300, 100);
    assert.deepStrictEqual(m.grid(), pinched);
    m.move(2, 310, 100);
    assert.strictEqual(m.grid().cellSize, pinched.cellSize);
});

test('mover un puntero desconocido no cambia nada', () => {
    const { m, changes } = mode();
    m.move(9, 10, 10);
    assert.strictEqual(changes.length, 0);
});

test('un tercer dedo no altera el pellizco de los dos primeros', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.down(2, 200, 100);
    m.down(3, 500, 500);
    m.move(3, 600, 600);
    assert.strictEqual(m.grid().cellSize, 70);
});

test('pellizcar y desplazar a la vez cambia el tamaño y arrastra con el punto medio', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.down(2, 200, 100);
    m.move(1, 140, 100);
    m.move(2, 340, 100); // distancia ×2, punto medio 100 px de cliente a la derecha: +50 de mapa
    const grid = m.grid();
    assert.strictEqual(grid.cellSize, 140);
    // El ancla (mapa 75) conserva su fracción de casilla (75/70 -> 0,0714...) y luego se traslada 45 de mapa.
    const expected = (((75 - (75 / 70) * 140) + 45) % 140 + 140) % 140;
    assert.ok(Math.abs(grid.offsetX - expected) < 1e-9, `${grid.offsetX} != ${expected}`);
});

test('reset olvida los dedos: un dedo nuevo desplaza en vez de pellizcar contra un fantasma', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.reset(); // pointerup perdido
    m.down(2, 300, 300);
    m.move(2, 320, 300);
    assert.deepStrictEqual(m.grid(), { cellSize: 70, offsetX: 10, offsetY: 0 });
});

test('sin reset un dedo fantasma convierte el arrastre en pellizco', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.down(2, 300, 300);
    m.move(2, 320, 300);
    assert.notStrictEqual(m.grid().cellSize, 70);
});

test('soltar todos los dedos y volver a empezar parte de la cuadrícula actual', () => {
    const { m } = mode();
    m.down(1, 100, 100);
    m.move(1, 120, 100);
    m.up(1);
    m.down(1, 500, 500);
    m.move(1, 520, 500);
    assert.deepStrictEqual(m.grid(), { cellSize: 70, offsetX: 20, offsetY: 0 });
});
