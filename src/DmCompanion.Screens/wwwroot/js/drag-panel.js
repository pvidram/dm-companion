// Paneles flotantes que se arrastran por su cabecera (la tarjeta del token). window.dragPanel.attach.
// Pointer Events con setPointerCapture sobre el asa `[data-drag-handle]` (touch-action: none, stopPropagation y preventDefault)
// para que el gesto nunca llegue al lienzo del mapa. JS mueve solo left/top del panel; la posición inicial la fija C#.
(function (root) {
    'use strict';

    const MARGIN = 16;
    const INTERACTIVE = 'button, input, select, textarea, a';

    // Posición (esquina superior izquierda) acotada al área con un margen; si no cabe gana el margen izquierdo/superior.
    function clampPanelPosition(x, y, panel, area, margin) {
        const clamp = (value, size, room) => Math.max(margin, Math.min(value, room - size - margin));
        return { x: clamp(x, panel.width, area.width), y: clamp(y, panel.height, area.height) };
    }

    function place(panel, x, y) {
        panel.style.left = x + 'px';
        panel.style.top = y + 'px';
    }

    function areaOf(panel) {
        const parent = panel.offsetParent || panel.parentElement;
        return { width: parent.clientWidth, height: parent.clientHeight };
    }

    // Reacota el panel abierto al área actual (rotación, tira expandida); un panel oculto no se toca.
    function reclamp(panel) {
        if (!panel.offsetParent) return;
        const next = clampPanelPosition(panel.offsetLeft, panel.offsetTop,
            { width: panel.offsetWidth, height: panel.offsetHeight }, areaOf(panel), MARGIN);
        place(panel, next.x, next.y);
    }

    // Conecta el arrastre del asa al panel. Devuelve { reset } para soltar un gesto a medias (el panel se oculta
    // durante un arrastre y el navegador ya no entrega pointerup): sin ello el asa quedaría muerta.
    function bind(panel, handle) {
        let drag = null;

        function onDown(event) {
            if (drag || (event.button !== undefined && event.button > 0)) return;
            if (event.target.closest && event.target.closest(INTERACTIVE)) return;
            event.stopPropagation();
            event.preventDefault();
            try {
                handle.setPointerCapture(event.pointerId);
            } catch (e) {
                return; // puntero ya inactivo: no hay arrastre que empezar
            }
            drag = { id: event.pointerId, startX: event.clientX, startY: event.clientY, left: panel.offsetLeft, top: panel.offsetTop };
        }

        function onMove(event) {
            if (!drag || event.pointerId !== drag.id) return;
            event.stopPropagation();
            event.preventDefault();
            const next = clampPanelPosition(
                drag.left + event.clientX - drag.startX, drag.top + event.clientY - drag.startY,
                { width: panel.offsetWidth, height: panel.offsetHeight }, areaOf(panel), MARGIN);
            place(panel, next.x, next.y);
        }

        function onEnd(event) {
            if (!drag || event.pointerId !== drag.id) return;
            event.stopPropagation();
            drag = null;
        }

        handle.addEventListener('pointerdown', onDown);
        handle.addEventListener('pointermove', onMove);
        handle.addEventListener('pointerup', onEnd);
        handle.addEventListener('pointercancel', onEnd);
        handle.addEventListener('lostpointercapture', onEnd);
        return { reset() { drag = null; } };
    }

    const api = { clampPanelPosition, bind };
    root.dragPanelMath = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (typeof window === 'undefined') return;

    const attached = new WeakMap();

    // Registra el arrastre una sola vez por panel y lo coloca en (x, y): cada apertura vuelve a salir junto al token.
    function attach(panel, x, y) {
        if (!panel) return;
        place(panel, x, y);
        const known = attached.get(panel);
        if (known) {
            known.reset();
            return;
        }
        const handle = panel.querySelector('[data-drag-handle]');
        if (!handle) return;
        attached.set(panel, bind(panel, handle));
        if (typeof ResizeObserver !== 'undefined' && panel.parentElement) {
            new ResizeObserver(() => reclamp(panel)).observe(panel.parentElement);
        }
    }

    window.dragPanel = { attach };
})(typeof window !== 'undefined' ? window : globalThis);
