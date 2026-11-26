// Capas de fondo, cuadrícula y trazos del lienzo del mapa (map-canvas.js), y el repintado de todas.
(function (root) {
    'use strict';

    const { theme, mapSize } = root.mapCore;
    const { drawTokens } = root.mapTokens;

    // Anchura mínima (px de pantalla) con la que la goma "toca" un trazo, para no exigir puntería de píxel.
    const ERASER_HIT_SCREEN_PX = 24;

    function drawBackground(inst) {
        const layer = inst.layers.background;
        layer.destroyChildren();
        const { w, h } = mapSize(inst);
        if (inst.bgImage) {
            layer.add(new Konva.Image({ image: inst.bgImage, x: 0, y: 0, width: w, height: h, listening: false }));
        } else if (w > 0 && h > 0) {
            const colors = theme(inst.host);
            layer.add(new Konva.Rect({
                x: 0, y: 0, width: w, height: h, listening: false,
                fill: colors.sheet, stroke: colors.outlineDark, strokeWidth: 2, strokeScaleEnabled: false,
            }));
        }
        layer.batchDraw();
    }

    function drawGrid(inst) {
        const layer = inst.layers.grid;
        layer.destroyChildren();
        // Con el ajuste con los dedos en curso se pinta la cuadrícula provisional; C# aún no la conoce.
        const grid = inst.adjust ? { ...inst.dto.grid, ...inst.adjust.grid() } : inst.dto.grid;
        const { w, h } = mapSize(inst);
        if (grid && grid.enabled && grid.cellSize >= 4 && w > 0 && h > 0) {
            const colors = theme(inst.host);
            const cell = grid.cellSize;
            const startX = ((grid.offsetX % cell) + cell) % cell;
            const startY = ((grid.offsetY % cell) + cell) % cell;
            layer.add(new Konva.Shape({
                listening: false,
                stroke: colors.outlineDark, opacity: 0.4, strokeWidth: 1, strokeScaleEnabled: false,
                sceneFunc(context, shape) {
                    context.beginPath();
                    for (let x = startX; x <= w; x += cell) { context.moveTo(x, 0); context.lineTo(x, h); }
                    for (let y = startY; y <= h; y += cell) { context.moveTo(0, y); context.lineTo(w, y); }
                    context.fillStrokeShape(shape);
                },
            }));
        }
        layer.batchDraw();
    }

    // Grosor del segmento entre dos puntos: width × (0.5 + presión media). Sin presión real se guardó 0.5 (grosor ×1).
    function segmentWidth(width, pressureA, pressureB) {
        return width * (0.5 + (pressureA + pressureB) / 2);
    }

    // `points` son tríos x, y, presión. Con `id` el trazo escucha eventos (solo lo usa la goma, vía la capa).
    function strokeNode(points, color, width, id) {
        return new Konva.Shape({
            listening: !!id, strokeId: id,
            sceneFunc(context) {
                context.setAttr('strokeStyle', color);
                context.setAttr('lineCap', 'round');
                context.setAttr('lineJoin', 'round');
                for (let i = 3; i + 2 < points.length; i += 3) {
                    context.beginPath();
                    context.moveTo(points[i - 3], points[i - 2]);
                    context.lineTo(points[i], points[i + 1]);
                    context.setAttr('lineWidth', segmentWidth(width, points[i - 1], points[i + 2]));
                    context.stroke();
                }
            },
            // Zona de toque de la goma: una sola polilínea, más ancha que el trazo y nunca menor que ~24 px de pantalla.
            hitFunc(context, shape) {
                const scale = shape.getAbsoluteScale().x || 1;
                context.setAttr('strokeStyle', shape.colorKey);
                context.setAttr('lineCap', 'round');
                context.setAttr('lineJoin', 'round');
                context.setAttr('lineWidth', Math.max(width * 1.5, ERASER_HIT_SCREEN_PX / scale));
                context.beginPath();
                context.moveTo(points[0], points[1]);
                for (let i = 3; i + 1 < points.length; i += 3) context.lineTo(points[i], points[i + 1]);
                if (points.length === 3) context.lineTo(points[0], points[1]);
                context.stroke();
            },
        });
    }

    function drawStrokes(inst) {
        const layer = inst.layers.strokes;
        layer.destroyChildren();
        inst.erased.clear();
        for (const stroke of inst.dto.strokes || []) layer.add(strokeNode(stroke.points, stroke.color, stroke.width, stroke.id));
        layer.batchDraw();
    }

    function drawAll(inst) {
        drawBackground(inst);
        drawGrid(inst);
        drawStrokes(inst);
        drawTokens(inst);
        window.fogMode.draw(inst);
    }

    root.mapLayers = { drawGrid, drawStrokes, strokeNode, drawAll };
})(window);
