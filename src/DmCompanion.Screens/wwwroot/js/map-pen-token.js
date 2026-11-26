// Lápiz sobre un token en Dibujar (map-canvas.js): espera a saber si es toque, coger o trazo.
(function (root) {
    'use strict';

    const { notify, containerPoint, worldPoint, eventPoints } = root.mapCore;
    const { notifyTokenTapped } = root.mapTokens;
    const { cancelGesture, strokeGesture } = root.mapGestures;

    // Lápiz apoyado sobre un token en Dibujar: espera a saber si es toque, coger o trazo (`penTokenGesture`).
    function tokenUnderPen(inst, event) {
        const point = worldPoint(inst, containerPoint(inst, event));
        const groups = inst.layers.tokens.getChildren().filter(g => g.draggable());
        const circles = groups.map(g => ({ id: g.id(), x: g.x(), y: g.y(), radius: g.getAttr('hitRadius') || 0 }));
        return window.penTokenGesture.tokenAt(point, circles);
    }

    // El grupo se busca por id en cada uso: un repintado de C# reconstruye la capa y deja obsoleta cualquier referencia.
    function penGroup(inst, gesture) {
        return inst.layers.tokens.findOne('#' + gesture.tokenId);
    }

    function applyGrab(inst, gesture, group) {
        if (gesture.zIndex === undefined) gesture.zIndex = group.zIndex();
        group.moveToTop();
        group.scale({ x: 1.12, y: 1.12 });
        group.position(gesture.position);
        inst.layers.tokens.batchDraw();
    }

    const penTokenGesture = {
        start(inst, gesture, event) {
            const group = penGroup(inst, gesture);
            gesture.startedAt = performance.now();
            gesture.startEvent = { clientX: event.clientX, clientY: event.clientY, pressure: event.pressure, type: 'pointermove' };
            gesture.startWorld = worldPoint(inst, containerPoint(inst, event));
            gesture.home = { x: group.x(), y: group.y() };
            gesture.position = gesture.home;
            gesture.phase = 'pending';
            gesture.distance = 0;
            gesture.buffered = [];
            gesture.timer = setTimeout(() => {
                gesture.timer = null;
                if (inst.gesture !== gesture) return;
                const action = window.penTokenGesture.penTimerAction(gesture.phase, gesture.distance);
                if (action === 'grab') {
                    gesture.phase = 'grab';
                    const live = penGroup(inst, gesture);
                    if (live) applyGrab(inst, gesture, live); else cancelGesture(inst);
                }
            }, window.penTokenGesture.PEN_HOLD_MS);
        },
        move(inst, gesture, event) {
            const pen = window.penTokenGesture;
            const group = penGroup(inst, gesture);
            if (!group) { cancelGesture(inst); return; } // el token ya no está (repintado, quitado): se abandona sin avisar
            if (gesture.phase === 'grab') {
                gesture.position = pen.grabPosition(gesture.home, gesture.startWorld, worldPoint(inst, containerPoint(inst, event)));
                applyGrab(inst, gesture, group);
                return;
            }
            gesture.distance = Math.hypot(event.clientX - gesture.startEvent.clientX, event.clientY - gesture.startEvent.clientY);
            // Mientras se decide, los puntos se guardan: si es trazo, empieza con todos; si no, se descartan.
            if (gesture.phase === 'pending') gesture.buffered = pen.bufferPenPoints(gesture.buffered, eventPoints(event));
            const action = pen.penMoveAction(gesture.phase, performance.now() - gesture.startedAt, gesture.distance);
            if (action !== 'stroke') return;
            // Se movió antes de tiempo: es un trazo que empieza donde se apoyó el lápiz y sigue por los puntos guardados.
            const points = pen.pendingStrokePoints(action, gesture.startEvent, gesture.buffered);
            gesture.buffered = [];
            penTokenGesture.release(inst, gesture);
            gesture.kind = 'stroke';
            gesture.handler = strokeGesture;
            strokeGesture.start(inst, gesture, points[0]);
            for (const p of points.slice(1)) strokeGesture.move(inst, gesture, p);
        },
        end(inst, gesture) {
            const group = penGroup(inst, gesture);
            const action = window.penTokenGesture.penReleaseAction(gesture.phase, performance.now() - gesture.startedAt, gesture.distance);
            penTokenGesture.release(inst, gesture);
            if (!group) return;
            // El lápiz no pasa por la regla del pellizco de los dedos (`inst.pinching`): suelta y guarda.
            if (action === 'commit') {
                if (window.penTokenGesture.hasMoved(gesture.home, gesture.position)) notify(inst, 'OnTokenMoved', gesture.tokenId, gesture.position.x, gesture.position.y);
            } else if (action === 'tap') notifyTokenTapped(inst, group, gesture.tokenId);
        },
        // Gesto cancelado (pointercancel, cambio de modo, dispose): si el token estaba cogido, vuelve a su sitio.
        cancel(inst, gesture) {
            const wasGrabbed = gesture.phase === 'grab';
            penTokenGesture.release(inst, gesture);
            const group = wasGrabbed ? penGroup(inst, gesture) : null;
            if (group) {
                group.position(gesture.home);
                inst.layers.tokens.batchDraw();
            }
        },
        // Temporizador y resalte fuera (escala y orden de dibujo como estaban).
        release(inst, gesture) {
            if (gesture.timer) clearTimeout(gesture.timer);
            gesture.timer = null;
            if (gesture.phase === 'grab') {
                const group = penGroup(inst, gesture);
                if (group) {
                    group.scale({ x: 1, y: 1 });
                    const last = inst.layers.tokens.getChildren().length - 1;
                    group.zIndex(Math.min(gesture.zIndex, last));
                    inst.layers.tokens.batchDraw();
                }
            }
            gesture.phase = 'done';
        },
    };

    root.mapPenToken = { penTokenGesture, tokenUnderPen };
})(window);
