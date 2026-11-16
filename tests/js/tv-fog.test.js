const test = require('node:test');
const assert = require('node:assert');
const { fogPolygons } = require('../../src/DmCompanion.Screens/wwwroot/js/tv/tv-layout.js');

test('fogPolygons descarta los polígonos con menos de 3 puntos o longitud impar', () => {
    assert.deepStrictEqual(fogPolygons({ fog: [[0, 0, 10, 0, 10, 10], [1, 2], [0, 0, 1]] }), [[0, 0, 10, 0, 10, 10]]);
});

test('fogPolygons sin niebla devuelve una lista vacía', () => {
    assert.deepStrictEqual(fogPolygons({}), []);
});
