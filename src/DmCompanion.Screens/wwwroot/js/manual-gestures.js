// Gestos del visor de manuales: un dedo desplaza el host (y, si la página cabe a lo ancho,
// desliza para pasar de página); dos dedos, pellizco para el zoom; en PC, Ctrl + rueda. Devuelve la función que los quita.
import './manual-geometry.js';

const SWIPE_PX = 60;
const WHEEL_ZOOM_STEP = 1.1;

export function attachGestures(canvas, host, { isBlocked, getZoom, clampZoom, onZoom, onSwipe }) {
    const pointers = new Map();
    let gesture = null;

    const down = e => {
        if (isBlocked()) return;
        canvas.setPointerCapture(e.pointerId);
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        gesture = pointers.size === 1
            ? { kind: 'pan', startX: e.clientX, startY: e.clientY, lastX: e.clientX, lastY: e.clientY }
            : { kind: 'pinch', startDistance: distance(pointers), startZoom: getZoom(), zoom: getZoom() };
    };

    const move = e => {
        if (!gesture || !pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (gesture.kind === 'pan') {
            host.scrollBy(gesture.lastX - e.clientX, gesture.lastY - e.clientY);
            gesture.lastX = e.clientX;
            gesture.lastY = e.clientY;
        } else if (pointers.size === 2 && gesture.startDistance > 0) {
            gesture.zoom = clampZoom(gesture.startZoom * distance(pointers) / gesture.startDistance);
            canvas.style.transformOrigin = '0 0';
            canvas.style.transform = `scale(${gesture.zoom / getZoom()})`; // Vista previa barata; se repinta al soltar.
        }
    };

    const up = e => {
        if (!pointers.delete(e.pointerId) || !gesture || pointers.size > 0) return;
        const ended = gesture;
        gesture = null;
        if (ended.kind === 'pinch') {
            canvas.style.transform = '';
            onZoom(ended.zoom);
            return;
        }
        const fitsWidth = host.scrollWidth <= host.clientWidth + 1;
        const direction = fitsWidth && e.type === 'pointerup'
            ? globalThis.manualGeometry.swipeDirection(e.clientX - ended.startX, e.clientY - ended.startY, SWIPE_PX)
            : 0;
        if (direction !== 0) onSwipe(direction);
    };

    // La rueda sola desplaza (lo hace el navegador); con Ctrl amplía o reduce la página en vez de todo el documento.
    const wheel = e => {
        if (!e.ctrlKey || isBlocked() || e.deltaY === 0) return;
        e.preventDefault();
        onZoom(clampZoom(getZoom() * (e.deltaY < 0 ? WHEEL_ZOOM_STEP : 1 / WHEEL_ZOOM_STEP)));
    };

    const handlers = [['pointerdown', down], ['pointermove', move], ['pointerup', up], ['pointercancel', up]];
    handlers.forEach(([type, handler]) => canvas.addEventListener(type, handler));
    canvas.addEventListener('wheel', wheel, { passive: false });
    return () => {
        handlers.forEach(([type, handler]) => canvas.removeEventListener(type, handler));
        canvas.removeEventListener('wheel', wheel);
    };
}

function distance(pointers) {
    const [a, b] = [...pointers.values()];
    return b ? Math.hypot(a.x - b.x, a.y - b.y) : 0;
}
