// Gestos de puntero del lienzo del mapa (map-canvas.js): trazo, goma y medición.
(function (root) {
    'use strict';

    const { ignore, notify, theme, containerPoint, worldPoint, eventPoints, pressureOf } = root.mapCore;
    const { strokeNode } = root.mapLayers;

    // Un punto de trazo a menos de este número de px de pantalla del anterior no aporta nada.
    const MIN_POINT_SCREEN_PX = 1;

    function clearDraft(inst) {
        inst.layers.draft.destroyChildren();
        inst.layers.draft.batchDraw();
    }

    function cancelGesture(inst) {
        const gesture = inst.gesture;
        if (!gesture) return;
        inst.gesture = null;
        if (gesture.handler.cancel) gesture.handler.cancel(inst, gesture);
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

    root.mapGestures = { clearDraft, cancelGesture, strokeGesture, eraseGesture, measureGesture };
})(window);
