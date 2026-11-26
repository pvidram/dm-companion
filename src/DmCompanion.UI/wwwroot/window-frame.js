// Marcos que se mueven por su cabecera `[data-drag-handle]` y se redimensionan con `[data-resize-handle="e|s|se"]`
// (paneles del carril y ventanas flotantes). Pointer Events con setPointerCapture, touch-action: none,
// stopPropagation y preventDefault: el gesto nunca llega al lienzo del mapa.
// La geometría se informa a C# solo al soltar (OnFrameChanged); reacotar tras un cambio del contenedor no informa.
const INTERACTIVE = 'button, input, select, textarea, a';
const MOVE_SLOP = 4;
const frames = new WeakMap();

const clamp = (value, min, max) => Math.max(min, Math.min(value, max));

// Tamaño entre el mínimo y el área menos el margen por lado; si el área no llega al mínimo, gana el área.
export function clampSize(size, min, area, margin) {
    const fit = (value, minimum, room) => {
        const max = room - 2 * margin;
        return max < minimum ? max : clamp(value, minimum, max);
    };
    return { width: fit(size.width, min.width, area.width), height: fit(size.height, min.height, area.height) };
}

// Posición (esquina superior izquierda) acotada al área con un margen; si no cabe gana el margen izquierdo/superior.
export function clampPosition(x, y, size, area, margin) {
    const fit = (value, extent, room) => Math.max(margin, Math.min(value, room - extent - margin));
    return { x: fit(x, size.width, area.width), y: fit(y, size.height, area.height) };
}

export function resizeFrom(edge, startSize, dx, dy) {
    return {
        width: edge === 's' ? startSize.width : startSize.width + dx,
        height: edge === 'e' ? startSize.height : startSize.height + dy,
    };
}

// Esquina del área más cercana al centro del marco y distancias del borde del marco al borde del área en esa esquina.
export function toCornerAnchor(x, y, size, area) {
    const right = x + size.width / 2 > area.width / 2;
    const bottom = y + size.height / 2 > area.height / 2;
    return {
        corner: (bottom ? 'b' : 't') + (right ? 'r' : 'l'),
        dx: right ? area.width - (x + size.width) : x,
        dy: bottom ? area.height - (y + size.height) : y,
    };
}

export function fromCornerAnchor(anchor, size, area, margin) {
    const x = anchor.corner[1] === 'r' ? area.width - size.width - anchor.dx : anchor.dx;
    const y = anchor.corner[0] === 'b' ? area.height - size.height - anchor.dy : anchor.dy;
    return clampPosition(x, y, size, area, margin);
}

export function isDoubleTap(prev, next, maxMs = 300, maxDist = 10) {
    if (!prev || prev.moved || next.moved) return false;
    return next.t - prev.t <= maxMs && Math.hypot(next.x - prev.x, next.y - prev.y) <= maxDist;
}

function areaOf(frame) {
    const parent = frame.parentElement;
    return parent ? { width: parent.clientWidth, height: parent.clientHeight } : { width: 0, height: 0 };
}

const sizeOf = frame => ({ width: frame.offsetWidth, height: frame.offsetHeight });
const minOf = options => ({ width: options.minWidth, height: options.minHeight });

function place(frame, x, y) {
    frame.style.left = x + 'px';
    frame.style.top = y + 'px';
}

// Muestra el tamaño pedido acotado al área actual y recoloca el marco. El tamaño pedido no se toca:
// si el área encoge (rotación, tira) el marco se ve más pequeño y recupera el pedido cuando vuelve el sitio.
function apply(frame, state) {
    const { options, requested } = state;
    // Un eje sin tamaño elegido (null) vuelve al de la hoja de estilos.
    if (requested?.width == null) frame.style.width = '';
    if (requested?.height == null) frame.style.height = '';
    const area = areaOf(frame);
    if (area.width <= 0 || area.height <= 0) return; // contenedor oculto: no se toca
    if (requested) {
        const shown = clampSize({ width: requested.width ?? 0, height: requested.height ?? 0 }, minOf(options), area, options.margin);
        if (requested.width != null) frame.style.width = shown.width + 'px';
        if (requested.height != null) frame.style.height = shown.height + 'px';
    }
    if (!options.movable) return;
    const size = sizeOf(frame);
    if (state.anchor) {
        const p = fromCornerAnchor(state.anchor, size, area, options.margin);
        place(frame, p.x, p.y);
    } else if (state.position) {
        const p = clampPosition(state.position.x, state.position.y, size, area, options.margin);
        place(frame, p.x, p.y);
    }
}

// Avisa a C#. El Promise rechazado (referencia .NET ya liberada al cerrar el marco) se ignora a propósito.
function notify(state, name, arg) {
    const result = arg === undefined ? state.listener.invokeMethodAsync(name) : state.listener.invokeMethodAsync(name, arg);
    if (result && typeof result.catch === 'function') result.catch(() => {});
}

// Informa de la posición (si el marco se mueve) y, solo si el gesto fue un cambio de tamaño, del tamaño PEDIDO por eje
// (null = no elegido), nunca del mostrado: acotado por el área o por el contenido no es lo que eligió el usuario.
function report(frame, state, resized) {
    const { options } = state;
    const size = sizeOf(frame);
    let x = 0, y = 0, anchor = null;
    if (options.movable) {
        x = frame.offsetLeft;
        y = frame.offsetTop;
        anchor = toCornerAnchor(x, y, size, areaOf(frame));
        state.anchor = anchor;
        state.position = { x, y };
    }
    const requested = resized && state.requested ? { width: state.requested.width ?? null, height: state.requested.height ?? null } : null;
    notify(state, 'OnFrameChanged', { x, y, size: requested, anchor });
}

function reset(frame, state) {
    frame.style.width = '';
    frame.style.height = '';
    state.requested = null;
    notify(state, 'OnFrameReset');
    if (state.options.movable) apply(frame, state);
}

// Conecta un asa. kind: 'move' (cabecera) o el borde 'e' | 's' | 'se'.
function bindHandle(frame, state, handle, kind) {
    const isMove = kind === 'move';
    let lastTap = null;
    handle.style.touchAction = 'none';

    function onDown(event) {
        if (state.gesture || (event.button !== undefined && event.button > 0)) return;
        if (isMove && event.target.closest && event.target.closest(INTERACTIVE)) return;
        event.stopPropagation();
        event.preventDefault();
        try {
            handle.setPointerCapture(event.pointerId);
        } catch (e) {
            return; // puntero ya inactivo: no hay gesto que empezar
        }
        // El stopPropagation de arriba impide el onpointerdown delegado de Blazor y preventDefault el foco por compatibilidad:
        // se enfoca el marco a mano para que su onfocusin lo traiga al frente.
        if (frame.focus) frame.focus({ preventScroll: true });
        state.gesture = {
            id: event.pointerId, startX: event.clientX, startY: event.clientY, moved: false, resized: false,
            left: frame.offsetLeft, top: frame.offsetTop, size: sizeOf(frame),
        };
    }

    function onMove(event) {
        const g = state.gesture;
        if (!g || event.pointerId !== g.id) return;
        event.stopPropagation();
        event.preventDefault();
        const dx = event.clientX - g.startX, dy = event.clientY - g.startY;
        if (Math.hypot(dx, dy) > MOVE_SLOP) g.moved = true;
        const { options } = state;
        const area = areaOf(frame);
        if (isMove) {
            const p = clampPosition(g.left + dx, g.top + dy, sizeOf(frame), area, options.margin);
            place(frame, p.x, p.y);
            return;
        }
        g.resized = true;
        const next = clampSize(resizeFrom(kind, g.size, dx, dy), minOf(options), area, options.margin);
        state.requested = {
            width: kind === 's' ? state.requested?.width ?? null : next.width,
            height: kind === 'e' ? state.requested?.height ?? null : next.height,
        };
        if (kind !== 's') frame.style.width = next.width + 'px';
        if (kind !== 'e') frame.style.height = next.height + 'px';
        if (options.movable) {
            const p = clampPosition(frame.offsetLeft, frame.offsetTop, sizeOf(frame), area, options.margin);
            place(frame, p.x, p.y);
        }
    }

    function onEnd(event) {
        const g = state.gesture;
        if (!g || event.pointerId !== g.id) return;
        event.stopPropagation();
        state.gesture = null;
        if (g.moved) {
            lastTap = null;
            report(frame, state, g.resized);
            return;
        }
        if (isMove || event.type !== 'pointerup') return;
        const tap = { t: event.timeStamp ?? Date.now(), x: event.clientX, y: event.clientY, moved: false };
        if (isDoubleTap(lastTap, tap)) {
            lastTap = null;
            reset(frame, state);
        } else {
            lastTap = tap;
        }
    }

    const events = { pointerdown: onDown, pointermove: onMove, pointerup: onEnd, pointercancel: onEnd, lostpointercapture: onEnd };
    for (const [type, fn] of Object.entries(events)) handle.addEventListener(type, fn);
    return () => { for (const [type, fn] of Object.entries(events)) handle.removeEventListener(type, fn); };
}

function load(state, options) {
    state.options = options;
    state.gesture = null;
    state.requested = options.size ? { width: options.size.width ?? null, height: options.size.height ?? null } : null;
    state.anchor = options.anchor || null;
    state.position = options.position || null;
}

export function attach(element, options, listener) {
    if (!element) return;
    if (frames.has(element)) detach(element);
    const state = { listener, unbinders: [], observer: null };
    load(state, options);
    frames.set(element, state);
    if (options.movable) {
        const header = element.querySelector('[data-drag-handle]');
        if (header) state.unbinders.push(bindHandle(element, state, header, 'move'));
    }
    for (const handle of element.querySelectorAll('[data-resize-handle]')) {
        state.unbinders.push(bindHandle(element, state, handle, handle.getAttribute('data-resize-handle')));
    }
    if (typeof ResizeObserver !== 'undefined' && element.parentElement) {
        state.observer = new ResizeObserver(() => {
            if (element.isConnected === false) {
                detach(element); // el marco ya no está en el DOM: no hay nada que acotar
                return;
            }
            apply(element, state);
        });
        state.observer.observe(element.parentElement);
    }
    apply(element, state);
}

// Reaplica tamaño, posición y ancla (el ancla gana) y suelta cualquier gesto a medias.
export function update(element, options) {
    const state = frames.get(element);
    if (!state) return;
    load(state, options);
    apply(element, state);
}

export function detach(element) {
    const state = frames.get(element);
    if (!state) return;
    state.gesture = null;
    state.unbinders.forEach(unbind => unbind());
    if (state.observer) state.observer.disconnect();
    // El tamaño en línea es de este marco: al soltarlo (p. ej. Glosario de Wide a Full) la hoja de estilos vuelve a mandar.
    element.style.width = '';
    element.style.height = '';
    frames.delete(element);
}
