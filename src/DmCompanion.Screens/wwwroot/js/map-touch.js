// Gestos táctiles del lienzo del mapa (map-canvas.js): arrastre, pellizco y rueda para el zoom.
(function (root) {
    'use strict';

    const { zoomAt } = root.mapView;
    const { touchBlocked, stageDragVetoed } = root.mapMode;

    function distance(a, b) { return Math.hypot(a.x - b.x, a.y - b.y); }

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
            if (inst.adjust) return; // el pellizco cambia la casilla, no el zoom
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
            if (event.evt.touches && event.evt.touches.length === 0) {
                inst.touches.clear();
                if (inst.adjust) inst.adjust.reset();
            }
            inst.cancelPinch();
            // Un dedo sobrante tras el pellizco no debe arrastrar el mapa ni mover un token.
            if (inst.pinching) setTimeout(() => { inst.pinching = false; }, 0);
        });

        stage.on('wheel', event => {
            event.evt.preventDefault();
            if (inst.adjust) return;
            const pointer = stage.getPointerPosition();
            if (!pointer) return;
            const factor = event.evt.deltaY < 0 ? 1.1 : 1 / 1.1;
            zoomAt(inst, pointer, stage.scaleX() * factor);
        });
    }

    root.mapTouch = { bindGestures };
})(window);
