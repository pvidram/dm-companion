// Ajuste de la cuadrícula con los dedos: cálculo puro (sin DOM ni Konva) para probarlo con `node --test` (tests/js).
// Trabaja con {cellSize, offsetX, offsetY} en coordenadas del mapa. El desplazamiento se normaliza a [0, cellSize).
(function (root) {
    'use strict';

    const wrap = (value, cell) => ((value % cell) + cell) % cell;

    // Desplaza la cuadrícula (dxMap, dyMap) px del mapa. El módulo evita que arrastrar más de una casilla se atasque.
    function panGrid(grid, dxMap, dyMap) {
        return {
            cellSize: grid.cellSize,
            offsetX: wrap(grid.offsetX + dxMap, grid.cellSize),
            offsetY: wrap(grid.offsetY + dyMap, grid.cellSize),
        };
    }

    // Cambia el tamaño en la razón currentDistance / startDistance (dentro de limits.min..max) manteniendo
    // fijo el punto `anchorMap`: su posición dentro de la casilla no cambia, así la línea bajo los dedos no se escapa.
    function pinchGrid(grid, startDistance, currentDistance, anchorMap, limits) {
        if (!(startDistance > 0) || !(currentDistance > 0) || !isFinite(startDistance) || !isFinite(currentDistance)) {
            return { cellSize: grid.cellSize, offsetX: grid.offsetX, offsetY: grid.offsetY };
        }
        const cellSize = Math.min(limits.max, Math.max(limits.min, grid.cellSize * (currentDistance / startDistance)));
        const anchored = (anchor, offset) => wrap(anchor - ((anchor - offset) / grid.cellSize) * cellSize, cellSize);
        return { cellSize, offsetX: anchored(anchorMap.x, grid.offsetX), offsetY: anchored(anchorMap.y, grid.offsetY) };
    }

    const api = { panGrid, pinchGrid };
    root.gridAdjust = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
