// Entrada del lienzo del mapa (map-canvas.js): eventos de puntero, ajuste de la cuadrícula y aplicación del modo.
(function (root) {
    'use strict';

    const { containerPoint, worldPoint } = root.mapCore;
    const { drawStrokes } = root.mapLayers;
    const { effectiveMode, pensOnly, isErasing, drawsWith, cancelTouchGestures, modeKey } = root.mapMode;
    const { cancelGesture, strokeGesture, eraseGesture, measureGesture } = root.mapGestures;
    const { penTokenGesture, tokenUnderPen } = root.mapPenToken;

    function beginGesture(inst, event, kind, handler, extra) {
        inst.gesture = { id: event.pointerId, kind, handler, ...extra };
        // La captura va en el contenedor (donde se escuchan los eventos de puntero), no en el `content` de Konva.
        try { inst.host.setPointerCapture(event.pointerId); } catch { /* el puntero ya no existe */ }
        handler.start(inst, inst.gesture, event);
    }

    // ---- ajuste de la cuadrícula con los dedos (la lógica está en grid-adjust-mode.js) ----

    // Dedos y ratón (en DevHost) mueven la cuadrícula; el lápiz no hace nada en este modo.
    function adjustPointer(inst, phase, event) {
        if (event.pointerType === 'pen') return;
        const adjust = inst.adjust;
        if (phase === 'down') {
            try { inst.host.setPointerCapture(event.pointerId); } catch { /* el puntero ya no existe */ }
            adjust.down(event.pointerId, event.clientX, event.clientY);
        } else if (phase === 'move') {
            adjust.move(event.pointerId, event.clientX, event.clientY);
        } else {
            adjust.up(event.pointerId);
            try { inst.host.releasePointerCapture(event.pointerId); } catch { /* ya liberado */ }
        }
    }

    function onPointerDown(inst, event) {
        if (event.pointerType === 'touch') inst.touches.add(event.pointerId);
        if (event.pointerType === 'mouse' && event.button !== 0) return;
        if (inst.adjust) { adjustPointer(inst, 'down', event); return; }
        const mode = effectiveMode(inst);
        if (mode === 'Draw' && drawsWith(event)) {
            if (event.pointerType === 'pen') inst.lastPenAt = performance.now();
            if (inst.gesture) return;
            cancelTouchGestures(inst); // un pellizco o arrastre táctil en curso cede ante el lápiz
            const tokenId = event.pointerType === 'pen' && !isErasing(inst) ? tokenUnderPen(inst, event) : null;
            if (isErasing(inst)) beginGesture(inst, event, 'erase', eraseGesture);
            else if (tokenId !== null) beginGesture(inst, event, 'penToken', penTokenGesture, { tokenId });
            else beginGesture(inst, event, 'stroke', strokeGesture);
        } else if (mode === 'Fog' && drawsWith(event)) {
            if (event.pointerType === 'pen') inst.lastPenAt = performance.now();
            cancelTouchGestures(inst);
            window.fogMode.onPenDown(inst, worldPoint(inst, containerPoint(inst, event)));
        } else if (mode === 'Measure') {
            // Un segundo dedo no mide: cancela la medición y el pellizco de Konva hace zoom.
            if (inst.touches.size > 1) cancelGesture(inst);
            else if (!inst.gesture) beginGesture(inst, event, 'measure', measureGesture);
        }
    }

    function onPointerMove(inst, event) {
        if (inst.adjust) { adjustPointer(inst, 'move', event); return; }
        const gesture = inst.gesture;
        if (event.pointerType === 'pen' && pensOnly(effectiveMode(inst))) {
            inst.lastPenAt = performance.now();
            if (!gesture) cancelTouchGestures(inst); // el lápiz se acerca (hover) o llega: fuera la palma
        }
        if (gesture && gesture.id === event.pointerId) gesture.handler.move(inst, gesture, event);
    }

    function onPointerEnd(inst, event, completed) {
        inst.touches.delete(event.pointerId);
        if (inst.adjust) { adjustPointer(inst, 'up', event); return; }
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

    function applyMode(inst) {
        cancelGesture(inst);
        inst.touches.clear(); // un dedo fantasma de otro modo cancelaría la siguiente medición
        const mode = effectiveMode(inst);
        inst.appliedKey = modeKey(inst);
        inst.layers.tokens.listening(mode === 'Navigate');
        inst.layers.strokes.listening(isErasing(inst));
        inst.host.style.cursor = mode === 'Navigate' ? '' : mode === 'GridAdjust' ? 'move' : 'crosshair';
        drawStrokes(inst); // dibuja también el grafo de impacto que la goma necesita
        inst.fogActive = mode === 'Fog';
        window.fogMode.clearDraft(inst); // cambiar de modo descarta la zona en curso
        window.fogMode.draw(inst); // las destapadas solo se marcan en Niebla
    }

    root.mapInput = { bindInput, applyMode };
})(window);
