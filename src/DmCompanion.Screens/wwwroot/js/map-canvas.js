// Lienzo del mapa (Konva). Un único mapa a la vez: window.mapCanvas.{load, render, setMode, fit, viewCenter, hostRect, dispose}.
// Capas, de abajo arriba: imagen o lienzo, cuadrícula, trazos, tokens y borrador (trazo en curso o medición).
// El estado manda C#: JS pinta lo que recibe y solo informa de gestos (token movido o tocado, imagen cargada,
// trazo añadido o borrado). La medición no cruza el puente.
(function () {
    'use strict';

    const DEFAULT_WIDTH = 2800;
    const DEFAULT_HEIGHT = 2100;
    const MAX_ZOOM = 8;
    // Tras levantar el lápiz, el dedo sigue ignorado este tiempo: la palma suele tocar justo antes y después.
    const PALM_GRACE_MS = 300;
    // Un punto de trazo a menos de este número de px de pantalla del anterior no aporta nada.
    const MIN_POINT_SCREEN_PX = 1;
    // Anchura mínima (px de pantalla) con la que la goma "toca" un trazo, para no exigir puntería de píxel.
    const ERASER_HIT_SCREEN_PX = 24;
    // Sin esto Konva ignora el segundo dedo mientras se arrastra, y el pellizco no funciona.
    Konva.hitOnDragEnabled = true;

    let current = null;

    function ignore() { /* la llamada a C# puede fallar si el circuito se cerró; no hay nada que hacer */ }

    function notify(inst, method, ...args) {
        inst.dotnet.invokeMethodAsync(method, ...args).catch(ignore);
    }

    // Colores del design system (tokens semánticos), leídos en cada pintado para respetar el tema.
    function theme(host) {
        const root = host.closest('.ds-root') || document.documentElement;
        const css = getComputedStyle(root);
        const get = (name, fallback) => css.getPropertyValue(name).trim() || fallback;
        return {
            pc: get('--faction-pc', '#1f5fa8'),
            ally: get('--faction-ally', '#2f7d4a'),
            enemy: get('--faction-enemy', '#a42a2a'),
            outlineDark: get('--faction-outline-on-sheet', '#1b1712'),
            outlineLight: get('--faction-outline-on-wood', '#f2ead8'),
            sheet: get('--surface-sheet', '#e4d5b0'),
            sheetSunken: get('--surface-sheet-sunken', '#d3c29a'),
            text: get('--text-ink', '#1b1712'),
            font: css.fontFamily || 'sans-serif',
            measureDark: get('--map-measure-line-dark', '#2B2118'),
            measureLight: get('--map-measure-line-light', '#E8CF8E'),
            measureDarkWidth: parseFloat(get('--map-measure-width-dark', '6px')),
            measureLightWidth: parseFloat(get('--map-measure-width-light', '3px')),
            labelBg: get('--map-measure-label-bg', '#2B2118'),
            labelText: get('--map-measure-label-text', '#F2EAD8'),
            labelFont: get('--font-body', '') || css.fontFamily || 'sans-serif',
        };
    }

    function mapSize(inst) {
        return { w: inst.mapW, h: inst.mapH };
    }

    // ---- vista (pan y zoom) ----

    function clampScale(inst, scale) {
        const min = inst.fitScale > 0 ? inst.fitScale * 0.5 : 0.05;
        return Math.min(MAX_ZOOM, Math.max(min, scale));
    }

    // Zoom manteniendo fijo el punto `center` (coordenadas del contenedor).
    function zoomAt(inst, center, newScale) {
        const stage = inst.stage;
        const scale = clampScale(inst, newScale);
        const old = stage.scaleX();
        const world = { x: (center.x - stage.x()) / old, y: (center.y - stage.y()) / old };
        stage.scale({ x: scale, y: scale });
        stage.position({ x: center.x - world.x * scale, y: center.y - world.y * scale });
    }

    function fitView(inst) {
        const { w, h } = mapSize(inst);
        const vw = inst.stage.width();
        const vh = inst.stage.height();
        if (!(w > 0 && h > 0 && vw > 0 && vh > 0)) return false;
        const scale = Math.min(vw / w, vh / h);
        inst.fitScale = scale;
        inst.stage.scale({ x: scale, y: scale });
        inst.stage.position({ x: (vw - w * scale) / 2, y: (vh - h * scale) / 2 });
        return true;
    }

    function tryFit(inst) {
        if (inst.needsFit && fitView(inst)) inst.needsFit = false;
    }

    // ---- capas ----

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
        const grid = inst.dto.grid;
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

    function portrait(inst, url) {
        if (!url) return null;
        let entry = inst.portraits.get(url);
        if (!entry) {
            const image = new Image();
            entry = { image, loaded: false };
            inst.portraits.set(url, entry);
            image.onload = () => {
                entry.loaded = true;
                if (current === inst) drawTokens(inst);
            };
            image.onerror = ignore; // sin retrato se queda la inicial
            image.src = url;
        }
        return entry.loaded ? entry.image : null;
    }

    function tokenNode(inst, token, colors) {
        const cell = (inst.dto.grid && inst.dto.grid.cellSize) || 70;
        const radius = (token.size * cell) / 2;
        const ring = colors[token.faction] || colors.pc;
        const group = new Konva.Group({ x: token.x, y: token.y, draggable: !inst.dto.readOnly, id: token.id });

        // Anillo con doble contorno (claro y oscuro) para que se lea sobre cualquier imagen:
        // claro | oscuro | color del bando | oscuro | claro.
        const face = new Konva.Circle({ radius, fill: colors.sheetSunken, stroke: colors.outlineLight, strokeWidth: 7, strokeScaleEnabled: false });
        group.add(face);
        group.add(new Konva.Circle({ radius, stroke: colors.outlineDark, strokeWidth: 5, strokeScaleEnabled: false }));

        const inner = Math.max(1, radius - 3);
        const image = portrait(inst, token.portraitUrl);
        if (image) {
            const side = Math.min(image.naturalWidth, image.naturalHeight) || 1;
            const scale = (2 * inner) / side;
            group.add(new Konva.Circle({
                radius: inner,
                fillPatternImage: image,
                fillPatternRepeat: 'no-repeat',
                fillPatternScale: { x: scale, y: scale },
                fillPatternOffset: { x: image.naturalWidth / 2, y: image.naturalHeight / 2 },
            }));
        } else {
            const fontSize = Math.max(8, inner * 1.1);
            group.add(new Konva.Text({
                text: token.initial || '?', fontSize, fontFamily: colors.font, fontStyle: 'bold', fill: colors.text,
                width: inner * 2, height: inner * 2, offsetX: inner, offsetY: inner, align: 'center', verticalAlign: 'middle',
                listening: false,
            }));
        }
        group.add(new Konva.Circle({ radius, stroke: ring, strokeWidth: 3, strokeScaleEnabled: false, listening: false }));

        group.on('dragstart', () => group.moveToTop());
        group.on('dragend', () => {
            // Un pellizco que corta el arrastre no es un movimiento: se devuelve el token a su sitio.
            if (inst.pinching) {
                group.position({ x: token.x, y: token.y });
                inst.layers.tokens.batchDraw();
                return;
            }
            notify(inst, 'OnTokenMoved', token.id, group.x(), group.y());
        });
        // Konva no emite click/tap si hubo arrastre, así que arrastrar un token nunca abre su tarjeta.
        group.on('click tap', () => {
            const rect = inst.host.getBoundingClientRect();
            const center = group.getAbsolutePosition();
            notify(inst, 'OnTokenTapped', token.id, rect.left + center.x, rect.top + center.y);
        });
        return group;
    }

    function drawTokens(inst) {
        const layer = inst.layers.tokens;
        layer.destroyChildren();
        const colors = theme(inst.host);
        for (const token of inst.dto.tokens || []) layer.add(tokenNode(inst, token, colors));
        layer.batchDraw();
    }

    function drawAll(inst) {
        drawBackground(inst);
        drawGrid(inst);
        drawStrokes(inst);
        drawTokens(inst);
    }

    // ---- fondo ----

    function setCanvasSize(inst) {
        inst.mapW = inst.dto.width > 0 ? inst.dto.width : DEFAULT_WIDTH;
        inst.mapH = inst.dto.height > 0 ? inst.dto.height : DEFAULT_HEIGHT;
    }

    function backgroundKey(dto) {
        return dto.mapPath + '|' + (dto.imageUrl || ('canvas:' + dto.width + 'x' + dto.height));
    }

    function loadBackground(inst) {
        inst.bgImage = null;
        inst.needsFit = true;
        const ticket = ++inst.bgTicket;
        const dto = inst.dto;
        if (!dto.imageUrl) {
            setCanvasSize(inst);
            drawAll(inst);
            tryFit(inst);
            return;
        }

        // Hasta conocer el tamaño real de la imagen no hay nada que pintar ni encajar.
        inst.mapW = 0;
        inst.mapH = 0;
        drawAll(inst);
        const img = new Image();
        img.onload = async () => {
            if (inst.bgTicket !== ticket || current !== inst) return;
            const width = img.naturalWidth;
            const height = img.naturalHeight;
            let source = img;
            const side = Math.max(width, height);
            if (side > dto.maxImageSide) {
                // El WebView no pinta imágenes enormes: se reduce, pero el mapa conserva sus medidas originales.
                const k = dto.maxImageSide / side;
                try {
                    source = await createImageBitmap(img, {
                        resizeWidth: Math.max(1, Math.round(width * k)),
                        resizeHeight: Math.max(1, Math.round(height * k)),
                        resizeQuality: 'high',
                    });
                } catch {
                    source = img;
                }
                if (inst.bgTicket !== ticket || current !== inst) return;
            }
            inst.bgImage = source;
            inst.mapW = width;
            inst.mapH = height;
            drawAll(inst);
            tryFit(inst);
            notify(inst, 'OnImageLoaded', dto.mapPath, width, height);
        };
        img.onerror = () => {
            if (inst.bgTicket !== ticket || current !== inst) return;
            inst.mapW = DEFAULT_WIDTH;
            inst.mapH = DEFAULT_HEIGHT;
            drawAll(inst);
            tryFit(inst);
            notify(inst, 'OnImageFailed', dto.mapPath);
            notify(inst, 'OnImageLoaded', dto.mapPath, DEFAULT_WIDTH, DEFAULT_HEIGHT);
        };
        img.src = dto.imageUrl;
    }

    // ---- gestos ----

    function distance(a, b) { return Math.hypot(a.x - b.x, a.y - b.y); }

    // En solo lectura no se dibuja, aunque C# recordara Dibujar.
    function effectiveMode(inst) {
        return inst.dto.readOnly && inst.mode === 'Draw' ? 'Navigate' : inst.mode;
    }

    function isErasing(inst) {
        return effectiveMode(inst) === 'Draw' && !!(inst.tool && inst.tool.eraser);
    }

    // En Dibujar el lápiz dibuja. En DevHost el ratón hace de lápiz (no hay otra forma de probarlo en el navegador).
    function drawsWith(event) {
        return event.pointerType === 'pen' || event.pointerType === 'mouse';
    }

    // Rechazo de palma: en Dibujar el dedo ni mueve ni hace zoom mientras el lápiz está apoyado o acaba de estarlo.
    function touchBlocked(inst) {
        return effectiveMode(inst) === 'Draw'
            && (inst.gesture !== null || performance.now() - inst.lastPenAt < PALM_GRACE_MS);
    }

    // Corta el arrastre del mapa o de un token y el pellizco en curso.
    function cancelTouchGestures(inst) {
        inst.cancelPinch();
        if (inst.stage.isDragging()) inst.stage.stopDrag();
        for (const element of [...Konva.DD._dragElements.values()]) element.node.stopDrag();
    }

    // El arrastre del fondo es de Konva; aquí se le quita cuando el gesto es de otra herramienta.
    function stageDragVetoed(inst, native) {
        const mode = effectiveMode(inst);
        if (mode === 'Measure') return inst.gesture !== null;
        if (mode !== 'Draw') return false;
        if (inst.gesture !== null) return true; // lápiz (o ratón) apoyado
        return !!native && String(native.type).startsWith('touch') && performance.now() - inst.lastPenAt < PALM_GRACE_MS;
    }

    function bindGestures(inst) {
        const stage = inst.stage;
        let lastDistance = 0;
        let lastCenter = null;
        inst.cancelPinch = () => {
            lastDistance = 0;
            lastCenter = null;
        };

        function touchPoint(touch) {
            const rect = inst.host.getBoundingClientRect();
            return { x: touch.clientX - rect.left, y: touch.clientY - rect.top };
        }

        stage.on('dragstart', event => {
            if (event.target === stage && stageDragVetoed(inst, event.evt)) stage.stopDrag();
        });

        stage.on('touchmove', event => {
            const touches = event.evt.touches;
            if (!touches || touches.length !== 2) return;
            event.evt.preventDefault();
            if (touchBlocked(inst)) {
                inst.cancelPinch();
                return;
            }
            // Dos dedos: se corta cualquier arrastre en curso (del mapa o de un token) y se hace zoom.
            inst.pinching = true;
            if (stage.isDragging()) stage.stopDrag();
            for (const element of [...Konva.DD._dragElements.values()]) element.node.stopDrag();

            const p1 = touchPoint(touches[0]);
            const p2 = touchPoint(touches[1]);
            const center = { x: (p1.x + p2.x) / 2, y: (p1.y + p2.y) / 2 };
            const dist = distance(p1, p2);
            if (!lastCenter || !lastDistance) {
                lastCenter = center;
                lastDistance = dist;
                return;
            }
            const before = stage.scaleX();
            zoomAt(inst, center, before * (dist / lastDistance));
            // Desplazamiento de los dedos durante el pellizco.
            stage.position({ x: stage.x() + (center.x - lastCenter.x), y: stage.y() + (center.y - lastCenter.y) });
            lastDistance = dist;
            lastCenter = center;
        });

        stage.on('touchend touchcancel', event => {
            if (event.evt.touches && event.evt.touches.length >= 2) return;
            // Sin dedos en pantalla no puede quedar ninguno apuntado (un pointerup perdido dejaría uno fantasma).
            if (event.evt.touches && event.evt.touches.length === 0) inst.touches.clear();
            inst.cancelPinch();
            // Un dedo sobrante tras el pellizco no debe arrastrar el mapa ni mover un token.
            if (inst.pinching) setTimeout(() => { inst.pinching = false; }, 0);
        });

        stage.on('wheel', event => {
            event.evt.preventDefault();
            const pointer = stage.getPointerPosition();
            if (!pointer) return;
            const factor = event.evt.deltaY < 0 ? 1.1 : 1 / 1.1;
            zoomAt(inst, pointer, stage.scaleX() * factor);
        });
    }

    // ---- dibujo, goma y medición (eventos de puntero) ----

    // Posición del evento en el contenedor (px de pantalla) y en el mapa.
    function containerPoint(inst, event) {
        const rect = inst.host.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    function worldPoint(inst, screen) {
        const stage = inst.stage;
        const scale = stage.scaleX();
        return { x: (screen.x - stage.x()) / scale, y: (screen.y - stage.y()) / scale };
    }

    // Todos los puntos del evento, incluidos los que el navegador agrupó entre dos fotogramas.
    function eventPoints(event) {
        const coalesced = typeof event.getCoalescedEvents === 'function' ? event.getCoalescedEvents() : [];
        return coalesced.length > 0 ? coalesced : [event];
    }

    function pressureOf(event) {
        return event.pressure > 0 ? event.pressure : 0.5;
    }

    function clearDraft(inst) {
        inst.layers.draft.destroyChildren();
        inst.layers.draft.batchDraw();
    }

    function cancelGesture(inst) {
        const gesture = inst.gesture;
        if (!gesture) return;
        inst.gesture = null;
        if (gesture.kind !== 'erase') clearDraft(inst);
        try { inst.host.releasePointerCapture(gesture.id); } catch { /* el puntero ya no existe */ }
    }

    // Trazo: se pinta en el borrador mientras dura el gesto y se entrega a C# al soltar.
    const strokeGesture = {
        start(inst, gesture, event) {
            gesture.color = inst.tool.color;
            gesture.width = inst.tool.width;
            gesture.points = [];
            gesture.node = strokeNode(gesture.points, gesture.color, gesture.width, null);
            inst.layers.draft.add(gesture.node);
            strokeGesture.move(inst, gesture, event);
        },
        move(inst, gesture, event) {
            const scale = inst.stage.scaleX();
            for (const e of eventPoints(event)) {
                const p = worldPoint(inst, containerPoint(inst, e));
                const n = gesture.points.length;
                if (n >= 3 && Math.hypot(p.x - gesture.points[n - 3], p.y - gesture.points[n - 2]) * scale < MIN_POINT_SCREEN_PX) continue;
                // Al soltar, `pressure` es 0: se conserva la del punto anterior para que el último segmento no salte de grosor.
                const pressure = e.type === 'pointerup' && n >= 3 ? gesture.points[n - 1] : pressureOf(e);
                gesture.points.push(p.x, p.y, pressure);
            }
            inst.layers.draft.batchDraw();
        },
        end(inst, gesture) {
            const points = gesture.points;
            if (points.length === 0) { clearDraft(inst); return; }
            // Un toque sin arrastre es un punto: dos puntos iguales, que el extremo redondo pinta como círculo.
            if (points.length === 3) points.push(points[0], points[1], points[2]);
            // Provisional hasta que llegue el repintado de C# (que reconstruye esta capa); evita un parpadeo.
            // Si C# rechaza el trazo no habrá repintado: se retira aquí.
            const node = gesture.node;
            node.moveTo(inst.layers.strokes);
            inst.layers.strokes.batchDraw();
            inst.layers.draft.batchDraw();
            inst.dotnet.invokeMethodAsync('OnStrokeAdded', gesture.color, gesture.width, points)
                .then(accepted => {
                    if (accepted !== false) return;
                    node.destroy();
                    inst.layers.strokes.batchDraw();
                })
                .catch(ignore);
        },
    };

    // Goma: borra el trazo entero que toca el puntero. Solo la capa de trazos escucha en este modo.
    function eraseAt(inst, event) {
        for (const e of eventPoints(event)) {
            const hit = inst.stage.getIntersection(containerPoint(inst, e));
            const id = hit && hit.getAttr('strokeId');
            if (!id || inst.erased.has(id)) continue;
            inst.erased.add(id);
            hit.destroy();
            inst.layers.strokes.batchDraw();
            notify(inst, 'OnStrokeErased', id);
        }
    }

    const eraseGesture = {
        start(inst, gesture, event) { eraseAt(inst, event); },
        move(inst, gesture, event) { eraseAt(inst, event); },
        end() { /* cada trazo se avisó al tocarlo */ },
    };

    // Medición: línea con doble trazo (oscuro y claro, legible sobre cualquier imagen) y etiqueta. Se borra al soltar.
    const measureGesture = {
        start(inst, gesture, event) {
            const colors = theme(inst.host);
            const line = (color, width) => new Konva.Line({
                points: [0, 0, 0, 0], stroke: color, strokeWidth: width, strokeScaleEnabled: false,
                lineCap: 'round', listening: false,
            });
            gesture.from = worldPoint(inst, containerPoint(inst, event));
            gesture.dark = line(colors.measureDark, colors.measureDarkWidth);
            gesture.light = line(colors.measureLight, colors.measureLightWidth);
            gesture.label = new Konva.Label({ listening: false });
            gesture.label.add(new Konva.Tag({ fill: colors.labelBg, cornerRadius: 4 }));
            gesture.text = new Konva.Text({ text: '', fontFamily: colors.labelFont, fontSize: 16, fill: colors.labelText, padding: 6 });
            gesture.label.add(gesture.text);
            inst.layers.draft.add(gesture.dark, gesture.light, gesture.label);
            measureGesture.move(inst, gesture, event);
        },
        move(inst, gesture, event) {
            const to = worldPoint(inst, containerPoint(inst, event));
            const dto = inst.dto;
            gesture.dark.points([gesture.from.x, gesture.from.y, to.x, to.y]);
            gesture.light.points([gesture.from.x, gesture.from.y, to.x, to.y]);
            gesture.text.text(window.mapMeasure.measureText(dto.grid, dto.unitAmount, dto.unitLabel, gesture.from, to));
            // La etiqueta mantiene su tamaño en pantalla (contra-escala) y queda encima y a la derecha del extremo.
            const scale = inst.stage.scaleX();
            gesture.label.scale({ x: 1 / scale, y: 1 / scale });
            gesture.label.offset({ x: -12, y: gesture.text.height() + 12 });
            gesture.label.position(to);
            inst.layers.draft.batchDraw();
        },
        end(inst) { clearDraft(inst); },
    };

    function beginGesture(inst, event, kind, handler) {
        inst.gesture = { id: event.pointerId, kind, handler };
        // La captura va en el contenedor (donde se escuchan los eventos de puntero), no en el `content` de Konva.
        try { inst.host.setPointerCapture(event.pointerId); } catch { /* el puntero ya no existe */ }
        handler.start(inst, inst.gesture, event);
    }

    function onPointerDown(inst, event) {
        if (event.pointerType === 'touch') inst.touches.add(event.pointerId);
        if (event.pointerType === 'mouse' && event.button !== 0) return;
        const mode = effectiveMode(inst);
        if (mode === 'Draw' && drawsWith(event)) {
            if (event.pointerType === 'pen') inst.lastPenAt = performance.now();
            if (inst.gesture) return;
            cancelTouchGestures(inst); // un pellizco o arrastre táctil en curso cede ante el lápiz
            if (isErasing(inst)) beginGesture(inst, event, 'erase', eraseGesture);
            else beginGesture(inst, event, 'stroke', strokeGesture);
        } else if (mode === 'Measure') {
            // Un segundo dedo no mide: cancela la medición y el pellizco de Konva hace zoom.
            if (inst.touches.size > 1) cancelGesture(inst);
            else if (!inst.gesture) beginGesture(inst, event, 'measure', measureGesture);
        }
    }

    function onPointerMove(inst, event) {
        const gesture = inst.gesture;
        if (event.pointerType === 'pen' && effectiveMode(inst) === 'Draw') {
            inst.lastPenAt = performance.now();
            if (!gesture) cancelTouchGestures(inst); // el lápiz se acerca (hover) o llega: fuera la palma
        }
        if (gesture && gesture.id === event.pointerId) gesture.handler.move(inst, gesture, event);
    }

    function onPointerEnd(inst, event, completed) {
        inst.touches.delete(event.pointerId);
        if (event.pointerType === 'pen') inst.lastPenAt = performance.now();
        const gesture = inst.gesture;
        if (!gesture || gesture.id !== event.pointerId) return;
        if (completed) {
            if (gesture.kind !== 'measure') gesture.handler.move(inst, gesture, event);
            gesture.handler.end(inst, gesture);
            inst.gesture = null;
            try { inst.host.releasePointerCapture(event.pointerId); } catch { /* ya liberado */ }
        } else {
            cancelGesture(inst);
        }
    }

    function bindInput(inst) {
        const options = { signal: inst.abort.signal };
        inst.host.addEventListener('pointerdown', event => onPointerDown(inst, event), options);
        inst.host.addEventListener('pointermove', event => onPointerMove(inst, event), options);
        inst.host.addEventListener('pointerup', event => onPointerEnd(inst, event, true), options);
        inst.host.addEventListener('pointercancel', event => onPointerEnd(inst, event, false), options);
    }

    // Qué escucha cada capa según el modo: los tokens solo en Navegar, los trazos solo con la goma.
    function modeKey(inst) {
        return effectiveMode(inst) + '|' + (isErasing(inst) ? 'erase' : '');
    }

    function applyMode(inst) {
        cancelGesture(inst);
        inst.touches.clear(); // un dedo fantasma de otro modo cancelaría la siguiente medición
        const mode = effectiveMode(inst);
        inst.appliedKey = modeKey(inst);
        inst.layers.tokens.listening(mode === 'Navigate');
        inst.layers.strokes.listening(isErasing(inst));
        inst.host.style.cursor = mode === 'Navigate' ? '' : 'crosshair';
        drawStrokes(inst); // dibuja también el grafo de impacto que la goma necesita
    }

    // ---- API ----

    function dispose() {
        const inst = current;
        if (!inst) return;
        current = null;
        inst.bgTicket++;
        inst.abort.abort();
        inst.host.style.cursor = '';
        inst.observer.disconnect();
        inst.stage.destroy();
    }

    function load(host, dto, dotnet) {
        dispose();
        const stage = new Konva.Stage({
            container: host,
            width: host.clientWidth,
            height: host.clientHeight,
            draggable: true,
        });
        const layers = {
            background: new Konva.Layer({ listening: false }),
            grid: new Konva.Layer({ listening: false }),
            strokes: new Konva.Layer({ listening: false }),
            tokens: new Konva.Layer(),
            draft: new Konva.Layer({ listening: false }),
        };
        for (const layer of Object.values(layers)) stage.add(layer);

        const inst = {
            host, dotnet, stage, layers, dto,
            key: backgroundKey(dto),
            mode: 'Navigate', tool: null,
            mapW: 0, mapH: 0, fitScale: 0, needsFit: true,
            bgImage: null, bgTicket: 0, pinching: false,
            portraits: new Map(),
            // Dibujo, goma y medición: gesto en curso (o null), dedos apoyados, último evento del lápiz y trazos ya borrados.
            gesture: null, touches: new Set(), lastPenAt: -Infinity, erased: new Set(),
            abort: new AbortController(), appliedKey: 'Navigate|',
        };
        inst.observer = new ResizeObserver(() => {
            const width = host.clientWidth;
            const height = host.clientHeight;
            if (width > 0 && height > 0) stage.size({ width, height });
            tryFit(inst);
        });
        inst.observer.observe(host);
        current = inst;
        bindGestures(inst);
        bindInput(inst);
        loadBackground(inst);
    }

    function render(dto) {
        const inst = current;
        if (!inst) return;
        inst.dto = dto;
        const key = backgroundKey(dto);
        if (key !== inst.key) {
            inst.key = key;
            loadBackground(inst);
            return;
        }
        // Lienzo en blanco: el tamaño de C# puede cambiar sin cambiar de mapa (p. ej. SetImageSize tras un fallo).
        if (!dto.imageUrl) setCanvasSize(inst);
        drawAll(inst);
        // Pasar a solo lectura (u otro cambio que altere el modo efectivo) reajusta las capas; si no, no se
        // toca nada: este repintado llega también tras cada trazo y no debe cortar el siguiente.
        if (modeKey(inst) !== inst.appliedKey) applyMode(inst);
    }

    function setMode(mode, tool) {
        if (!current) return;
        current.mode = mode;
        current.tool = tool;
        applyMode(current);
    }

    function fit() {
        if (!current) return;
        current.needsFit = true;
        tryFit(current);
    }

    // Centro de la vista en coordenadas del mapa: .NET coloca ahí los tokens nuevos.
    function viewCenter() {
        if (!current) return null;
        const stage = current.stage;
        const scale = stage.scaleX();
        return [(stage.width() / 2 - stage.x()) / scale, (stage.height() / 2 - stage.y()) / scale];
    }

    // Área del mapa en el viewport (left, top, width, height): .NET convierte con ella el toque de un token a px del área.
    function hostRect() {
        if (!current) return null;
        const rect = current.host.getBoundingClientRect();
        return [rect.left, rect.top, rect.width, rect.height];
    }

    window.mapCanvas = { load, render, setMode, fit, viewCenter, hostRect, dispose };
})();
