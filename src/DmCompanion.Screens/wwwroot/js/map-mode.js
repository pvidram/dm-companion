// Modo efectivo del lienzo del mapa (map-canvas.js) y rechazo de palma.
(function (root) {
    'use strict';

    // Tras levantar el lápiz, el dedo sigue ignorado este tiempo: la palma suele tocar justo antes y después.
    const PALM_GRACE_MS = 300;

    // En solo lectura no se dibuja ni se toca la niebla, aunque C# recordara Dibujar o Niebla.
    function effectiveMode(inst) {
        if (inst.adjust) return 'GridAdjust';
        return inst.dto.readOnly && pensOnly(inst.mode) ? 'Navigate' : inst.mode;
    }

    // Modos en los que solo actúan el lápiz y el ratón; el dedo mueve y hace zoom.
    function pensOnly(mode) { return mode === 'Draw' || mode === 'Fog'; }

    function isErasing(inst) {
        return effectiveMode(inst) === 'Draw' && !!(inst.tool && inst.tool.eraser);
    }

    // En Dibujar el lápiz dibuja. En DevHost el ratón hace de lápiz (no hay otra forma de probarlo en el navegador).
    function drawsWith(event) {
        return event.pointerType === 'pen' || event.pointerType === 'mouse';
    }

    // Rechazo de palma: en Dibujar el dedo ni mueve ni hace zoom mientras el lápiz está apoyado o acaba de estarlo.
    function touchBlocked(inst) {
        // Al calibrar con un rectángulo el lápiz también traza en exclusiva: mismo rechazo de palma.
        if (inst.adjust && inst.adjust.zooms) return performance.now() - inst.lastPenAt < PALM_GRACE_MS;
        return pensOnly(effectiveMode(inst))
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
        if (mode === 'GridAdjust') return true; // los dedos mueven la cuadrícula, no el mapa
        if (mode === 'Measure') return inst.gesture !== null;
        // En Niebla el toque no abre gesto: el lápiz o el ratón no arrastran el mapa.
        if (mode === 'Fog' && native && (native.pointerType === 'pen' || native.pointerType === 'mouse' || String(native.type).startsWith('mouse'))) return true;
        if (!pensOnly(mode)) return false;
        if (inst.gesture !== null) return true; // lápiz (o ratón) apoyado
        return !!native && String(native.type).startsWith('touch') && performance.now() - inst.lastPenAt < PALM_GRACE_MS;
    }

    // Qué escucha cada capa según el modo: los tokens solo en Navegar, los trazos solo con la goma.
    function modeKey(inst) {
        return effectiveMode(inst) + '|' + (isErasing(inst) ? 'erase' : '');
    }

    root.mapMode = { effectiveMode, pensOnly, isErasing, drawsWith, touchBlocked, cancelTouchGestures, stageDragVetoed, modeKey };
})(window);
