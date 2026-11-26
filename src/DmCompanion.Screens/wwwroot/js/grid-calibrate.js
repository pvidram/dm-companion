// Calibrar la cuadrícula con un rectángulo: cálculo puro (sin DOM ni Konva) para probarlo con `node --test` (tests/js).
// Generaliza `GridGeometry.Calibrate` de `archivo/fase-2-mapa-v1` a columnas × filas. Trabaja en px de mapa.
(function (root) {
    'use strict';

    const wrap = (value, cell) => ((value % cell) + cell) % cell;
    const validCount = n => Number.isInteger(n) && n >= 1;

    // El rectángulo abarca `columns` × `rows` casillas. El tamaño es la media de lo que sale por ancho y por alto
    // (sin redondear); el desplazamiento es la esquina superior izquierda normalizada a [0, cellSize).
    function calibrateGrid(rect, columns, rows) {
        if (!validCount(columns) || !validCount(rows) || !rect) return null;
        const { x, y, width, height } = rect;
        if (![x, y, width, height].every(Number.isFinite) || width === 0 || height === 0) return null;
        const cellSize = (Math.abs(width) / columns + Math.abs(height) / rows) / 2;
        return {
            enabled: true,
            cellSize,
            offsetX: wrap(Math.min(x, x + width), cellSize),
            offsetY: wrap(Math.min(y, y + height), cellSize),
        };
    }

    const api = { calibrateGrid };
    root.gridCalibrate = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
