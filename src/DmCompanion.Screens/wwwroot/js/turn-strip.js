// Tira de turnos: reordenar arrastrando el asa «⋮⋮» de cada fila. window.turnStrip.{attach, detach}.
// Solo se escuchan Pointer Events sobre el asa (touch-action: none, stopPropagation y preventDefault) para que el gesto
// nunca llegue al lienzo del mapa. JS no mueve el DOM: al soltar avisa a C# (OnEntryMoved) y Blazor repinta la lista.
(function (root) {
    'use strict';

    // Posición final de la fila arrastrada: cuántas de las demás filas tienen su centro por encima del puntero.
    function dropIndex(midpoints, fromIndex, pointerY) {
        return midpoints.filter((m, i) => i !== fromIndex && m < pointerY).length;
    }

    const api = { dropIndex };
    root.turnStripMath = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (typeof window === 'undefined') return;

    let current = null;

    function ignore() { /* la llamada a C# puede fallar si el circuito se cerró; no hay nada que hacer */ }

    function rowsOf(list) {
        return Array.from(list.querySelectorAll(':scope > [data-entry-id]'));
    }

    function clearMarks(list) {
        list.querySelectorAll('.combat-strip__row--dragging, .combat-strip__row--drop-before, .combat-strip__row--drop-after')
            .forEach(row => row.classList.remove('combat-strip__row--dragging', 'combat-strip__row--drop-before', 'combat-strip__row--drop-after'));
    }

    function finish(inst, send) {
        const drag = inst.drag;
        if (!drag) return;
        inst.drag = null;
        inst.mutations.disconnect();
        clearMarks(inst.list);
        if (drag.handle.hasPointerCapture && drag.handle.hasPointerCapture(drag.pointerId)) {
            try { drag.handle.releasePointerCapture(drag.pointerId); } catch (e) { /* ya liberado */ }
        }
        if (send && drag.newIndex !== drag.fromIndex) {
            inst.dotnet.invokeMethodAsync('OnEntryMoved', drag.entryId, drag.newIndex).catch(ignore);
        }
    }

    function update(inst, y) {
        const drag = inst.drag;
        const rows = rowsOf(inst.list);
        const from = rows.findIndex(r => r.dataset.entryId === drag.entryId);
        if (from < 0) { finish(inst, false); return; }
        const mids = rows.map(r => { const b = r.getBoundingClientRect(); return b.top + b.height / 2; });
        drag.newIndex = dropIndex(mids, from, y);
        clearMarks(inst.list);
        rows[from].classList.add('combat-strip__row--dragging');
        const others = rows.filter((_, i) => i !== from);
        if (drag.newIndex < others.length) others[drag.newIndex].classList.add('combat-strip__row--drop-before');
        else if (others.length > 0) others[others.length - 1].classList.add('combat-strip__row--drop-after');
    }

    function onPointerDown(inst, event) {
        const handle = event.target.closest && event.target.closest('.combat-strip__handle');
        if (!handle || !inst.list.contains(handle) || inst.drag) return;
        const row = handle.closest('[data-entry-id]');
        if (!row) return;
        event.stopPropagation();
        event.preventDefault();
        const rows = rowsOf(inst.list);
        const fromIndex = rows.indexOf(row);
        inst.drag = { entryId: row.dataset.entryId, fromIndex, newIndex: fromIndex, pointerId: event.pointerId, handle };
        handle.setPointerCapture(event.pointerId);
        // Si Blazor repinta la lista a mitad de arrastre, las filas ya no son las mismas: se aborta.
        inst.mutations.observe(inst.list, { childList: true, subtree: true });
        update(inst, event.clientY);
    }

    function onPointerMove(inst, event) {
        if (!inst.drag || event.pointerId !== inst.drag.pointerId) return;
        event.stopPropagation();
        event.preventDefault();
        update(inst, event.clientY);
    }

    function onPointerUp(inst, event) {
        if (!inst.drag || event.pointerId !== inst.drag.pointerId) return;
        event.stopPropagation();
        event.preventDefault();
        finish(inst, true);
    }

    function onCancel(inst, event) {
        if (inst.drag && event.pointerId === inst.drag.pointerId) finish(inst, false);
    }

    function attach(list, dotnet) {
        detach();
        const inst = { list, dotnet, drag: null, abort: new AbortController() };
        inst.mutations = new MutationObserver(() => finish(inst, false));
        const options = { signal: inst.abort.signal };
        list.addEventListener('pointerdown', e => onPointerDown(inst, e), options);
        list.addEventListener('pointermove', e => onPointerMove(inst, e), options);
        list.addEventListener('pointerup', e => onPointerUp(inst, e), options);
        list.addEventListener('pointercancel', e => onCancel(inst, e), options);
        list.addEventListener('lostpointercapture', e => onCancel(inst, e), options);
        current = inst;
    }

    function detach() {
        const inst = current;
        if (!inst) return;
        current = null;
        finish(inst, false);
        inst.abort.abort();
    }

    window.turnStrip = { attach, detach };
})(typeof window !== 'undefined' ? window : globalThis);
