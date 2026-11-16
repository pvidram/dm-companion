// Mapa de la TV con Konva, solo lectura. Aspecto copiado de wwwroot/js/map-canvas.js (tokens, cuadrícula, trazos).
// Al recibir «map» se redibuja entero, sin animaciones. ES2017 sin módulos.
(function (root) {
    'use strict';

    var DEFAULT_WIDTH = 2800;
    var DEFAULT_HEIGHT = 2100;

    var RESIZE_DEBOUNCE_MS = 200;
    function theme() {
        var css = getComputedStyle(document.documentElement);
        function get(name, fallback) { return css.getPropertyValue(name).trim() || fallback; }
        return {
            pc: get('--faction-pc', '#3E6A8E'), ally: get('--faction-ally', '#4F7A4A'), enemy: get('--faction-enemy', '#9E2B25'),
            outlineDark: get('--faction-outline-on-sheet', '#2B2118'), outlineLight: get('--faction-outline-on-wood', '#E8DCC2'),
            accent: get('--accent-on-wood', '#C9A24A'), danger: get('--fill-damage', '#9E2B25'),
            fog: get('--tv-fog', '#1B1712'),
            sheet: get('--surface-sheet', '#D6C4A0'), sheetSunken: get('--surface-sheet-sunken', '#C9B48C'),
            text: get('--text-ink', '#2B2118'), textOnDanger: get('--text-on-wood', '#E8DCC2'),
            font: css.fontFamily || 'sans-serif'
        };
    }

    // Grosor del segmento entre dos puntos: width × (0.5 + presión media), como en la tablet.
    function segmentWidth(width, a, b) { return width * (0.5 + (a + b) / 2); }

    function strokeNode(points, color, width) {
        return new Konva.Shape({
            listening: false,
            sceneFunc: function (context) {
                context.setAttr('strokeStyle', color);
                context.setAttr('lineCap', 'round');
                context.setAttr('lineJoin', 'round');
                for (var i = 3; i + 2 < points.length; i += 3) {
                    context.beginPath();
                    context.moveTo(points[i - 3], points[i - 2]);
                    context.lineTo(points[i], points[i + 1]);
                    context.setAttr('lineWidth', segmentWidth(width, points[i - 1], points[i + 2]));
                    context.stroke();
                }
            }
        });
    }

    function gridNode(grid, w, h, colors) {
        var cell = grid.cellSize;
        var startX = ((grid.offsetX % cell) + cell) % cell;
        var startY = ((grid.offsetY % cell) + cell) % cell;
        return new Konva.Shape({
            listening: false, stroke: colors.outlineDark, opacity: 0.4, strokeWidth: 1, strokeScaleEnabled: false,
            sceneFunc: function (context, shape) {
                context.beginPath();
                for (var x = startX; x <= w; x += cell) { context.moveTo(x, 0); context.lineTo(x, h); }
                for (var y = startY; y <= h; y += cell) { context.moveTo(0, y); context.lineTo(w, y); }
                context.fillStrokeShape(shape);
            }
        });
    }

    function circle(radius, props) {
        props.radius = radius;
        props.strokeScaleEnabled = false;
        props.listening = false;
        return new Konva.Circle(props);
    }

    function addCombatMarks(group, token, radius, colors) {
        if (token.current) {
            group.add(circle(radius + 9, { stroke: colors.outlineDark, strokeWidth: 8 }));
            group.add(circle(radius + 9, { stroke: colors.accent, strokeWidth: 4 }));
        }
        if (token.conditions > 0) {
            var badgeRadius = 12;
            var offset = radius * Math.SQRT1_2;
            var badge = new Konva.Group({ x: offset, y: -offset, listening: false });
            badge.add(circle(badgeRadius, { fill: colors.sheet, stroke: colors.outlineDark, strokeWidth: 2 }));
            badge.add(new Konva.Text({
                text: String(token.conditions), fontSize: 14, fontFamily: colors.font, fontStyle: 'bold', fill: colors.text,
                width: badgeRadius * 2, height: badgeRadius * 2, offsetX: badgeRadius, offsetY: badgeRadius,
                align: 'center', verticalAlign: 'middle', listening: false
            }));
            group.add(badge);
        }
        if (token.dead) {
            var arm = radius * 0.55;
            var arms = [[-arm, -arm, arm, arm], [-arm, arm, arm, -arm]];
            for (var i = 0; i < arms.length; i++) {
                group.add(new Konva.Line({ points: arms[i], stroke: colors.outlineDark, strokeWidth: 9, lineCap: 'round', strokeScaleEnabled: false, listening: false }));
                group.add(new Konva.Line({ points: arms[i], stroke: colors.outlineLight, strokeWidth: 5, lineCap: 'round', strokeScaleEnabled: false, listening: false }));
            }
        }
    }

    // Un dibujante por contenedor: la TV monta uno; la tablet puede montar otro para la vista de jugadores.
    function create(options) {
        var container = options.container;
        var stage = null;
        var layer = null;
        var images = {}; // url -> HTMLImageElement ya cargada (o 'loading' / 'failed')
        var lastMap = null;
        var redrawQueued = false;
        var resizeTimer = null;
        var disposed = false;

        // Devuelve la imagen si ya está cargada; si no, la pide y repinta cuando llegue (sin cambiar lo que se ve entretanto).
        function image(url) {
            if (!url) return null;
            var cached = images[url];
            if (cached && cached !== 'loading' && cached !== 'failed') return cached;
            if (!cached) {
                images[url] = 'loading';
                var img = new Image();
                img.onload = function () { images[url] = img; scheduleRedraw(); };
                img.onerror = function () { images[url] = 'failed'; scheduleRedraw(); };
                img.src = url;
            }
            return null;
        }

        // Un solo repintado por fotograma aunque lleguen varias imágenes a la vez.
        function scheduleRedraw() {
            if (redrawQueued || disposed) return;
            redrawQueued = true;
            window.requestAnimationFrame(function () {
                redrawQueued = false;
                if (lastMap && !disposed) draw(lastMap);
            });
        }

        function ensureStage(area) {
            container.style.top = area.top + 'px';
            if (!stage) {
                stage = new Konva.Stage({ container: container, width: area.width, height: area.height, listening: false });
                layer = new Konva.Layer({ listening: false });
                stage.add(layer);
            }
            stage.size({ width: area.width, height: area.height });
        }

        function addFace(body, token, radius, colors) {
            var inner = Math.max(1, radius - 3);
            var portrait = image(token.portraitUrl);
            if (portrait) {
                var side = Math.min(portrait.naturalWidth, portrait.naturalHeight) || 1;
                var scale = (2 * inner) / side;
                body.add(new Konva.Circle({
                    radius: inner, listening: false, fillPatternImage: portrait, fillPatternRepeat: 'no-repeat',
                    fillPatternScale: { x: scale, y: scale }, fillPatternOffset: { x: portrait.naturalWidth / 2, y: portrait.naturalHeight / 2 }
                }));
            } else {
                body.add(new Konva.Text({
                    text: token.initial || '?', fontSize: Math.max(8, inner * 1.1), fontFamily: colors.font, fontStyle: 'bold', fill: colors.text,
                    width: inner * 2, height: inner * 2, offsetX: inner, offsetY: inner, align: 'center', verticalAlign: 'middle', listening: false
                }));
            }
        }

        function tokenNode(map, token, colors) {
            var cell = (map.grid && map.grid.cellSize) || 70;
            var radius = (token.size * cell) / 2;
            var group = new Konva.Group({ x: token.x, y: token.y, listening: false });
            var body = new Konva.Group({ opacity: root.tokenOpacity.tokenBodyOpacity(token), listening: false });
            group.add(body);
            body.add(circle(radius, { fill: colors.sheetSunken, stroke: colors.outlineLight, strokeWidth: 7 }));
            body.add(circle(radius, { stroke: colors.outlineDark, strokeWidth: 5 }));
            addFace(body, token, radius, colors);
            body.add(circle(radius, { stroke: colors[token.faction] || colors.pc, strokeWidth: 3 }));
            addCombatMarks(group, token, radius, colors);
            return group;
        }

        // Mensaje nuevo: los fallos de carga anteriores se olvidan para reintentarlos.
        function render(map) {
            for (var url in images) {
                if (images.hasOwnProperty(url) && images[url] === 'failed') delete images[url];
            }
            draw(map);
        }

        function draw(map) {
            lastMap = map;
            var area = options.viewport();
            ensureStage(area);
            var background = image(map.imageUrl);
            // Mientras la imagen del mapa carga se deja lo que hay en pantalla; si falla, se pinta el lienzo en blanco.
            if (map.imageUrl && !background && images[map.imageUrl] === 'loading') return;
            layer.destroyChildren();
            var colors = theme();
            var w = background ? background.naturalWidth : (map.width || DEFAULT_WIDTH);
            var h = background ? background.naturalHeight : (map.height || DEFAULT_HEIGHT);
            var frame = map.frame || { x: 0, y: 0, width: w, height: h };
            var t = root.tvLayout.stageTransform(frame, { width: area.width, height: area.height });
            layer.scale({ x: t.scale, y: t.scale });
            layer.position({ x: t.x, y: t.y });
            layer.clip({ x: frame.x, y: frame.y, width: frame.width, height: frame.height });

            if (background) layer.add(new Konva.Image({ image: background, x: 0, y: 0, width: w, height: h, listening: false }));
            else layer.add(new Konva.Rect({ x: 0, y: 0, width: w, height: h, fill: colors.sheet, listening: false }));
            if (map.grid && map.grid.enabled && map.grid.cellSize >= 4) layer.add(gridNode(map.grid, w, h, colors));
            var strokes = map.strokes || [];
            for (var i = 0; i < strokes.length; i++) layer.add(strokeNode(strokes[i].points, strokes[i].color, strokes[i].width));
            var tokens = map.tokens || [];
            for (var j = 0; j < tokens.length; j++) layer.add(tokenNode(map, tokens[j], colors));
            // La niebla va opaca encima de todo y en el mismo fotograma que el mapa: nada se asoma antes de taparse.
            var fog = root.tvLayout.fogPolygons(map);
            for (var k = 0; k < fog.length; k++) layer.add(new Konva.Line({ points: fog[k], closed: true, fill: colors.fog, listening: false }));
            layer.batchDraw();
        }

        function resize() {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(function () { if (lastMap && !disposed) draw(lastMap); }, RESIZE_DEBOUNCE_MS);
        }

        function dispose() {
            disposed = true;
            clearTimeout(resizeTimer);
            lastMap = null;
            if (stage) stage.destroy();
            stage = null;
            layer = null;
            images = {};
        }

        return { render: render, resize: resize, dispose: dispose };
    }

    root.tvMap = { create: create };
})(window);
