// Medición del mapa: función pura (sin DOM ni Konva) para poder probarla con `node --test` (tests/js).
// Misma fórmula que GridMath.DistanceFeet: máx(Δcasillas) × cantidad de la unidad del sistema de juego. Sin cuadrícula, px.
(function (root) {
    'use strict';

    function measureText(grid, unitAmount, unitLabel, from, to) {
        if (!grid || !grid.enabled || !(grid.cellSize > 0)) return Math.round(Math.hypot(to.x - from.x, to.y - from.y)) + ' px';
        const cell = (value, offset) => Math.floor((value - offset) / grid.cellSize);
        const steps = Math.max(
            Math.abs(cell(to.x, grid.offsetX) - cell(from.x, grid.offsetX)),
            Math.abs(cell(to.y, grid.offsetY) - cell(from.y, grid.offsetY)));
        const amount = steps * unitAmount;
        return (Number.isInteger(amount) ? amount : amount.toFixed(1)) + ' ' + unitLabel;
    }

    const api = { measureText };
    root.mapMeasure = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
