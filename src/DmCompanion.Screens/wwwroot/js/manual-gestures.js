// Gestos del visor de manuales: pellizco con dos dedos y, en PC, Ctrl + rueda, para el zoom. El desplazamiento
// (rueda sola, un dedo) lo hace el navegador sobre el host. Devuelve la función que los quita.
import './manual-scroll.js';

const { previewOrigin } = globalThis.manualScroll;
const WHEEL_ZOOM_STEP = 1.1;
const WHEEL_SETTLE_MS = 120;

export function attachGestures(host, column, { isBlocked, getZoom, clampZoom, onZoomStart, onZoom, onZoomCancel }) {
    const pointers = new Map();
    let pinch = null;
    let wheelZoom = null; // Zoom acumulado de la ráfaga de rueda en curso.
    let wheelFrame = 0;
    let wheelTimer = 0;
    let origin = '0 0'; // transform-origin de la vista previa: se fija al empezar el gesto, con el scroll de ese momento.

    const beginPreview = () => {
        origin = previewOrigin(host.scrollLeft, host.scrollTop, column.offsetLeft, column.offsetTop);
        onZoomStart();
    };

    // Solo dedos: un ratón o un lápiz no pellizcan, y un puntero «pegado» falsearía el gesto.
    const down = e => {
        if (e.pointerType !== 'touch' || isBlocked()) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size === 2) {
            host.setPointerCapture(e.pointerId);
            beginPreview();
            pinch = { startDistance: distance(pointers), startZoom: getZoom(), zoom: getZoom() };
        }
    };

    const move = e => {
        if (!pinch || !pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size === 2 && pinch.startDistance > 0) {
            pinch.zoom = clampZoom(pinch.startZoom * distance(pointers) / pinch.startDistance);
            column.style.transformOrigin = origin;
            column.style.transform = `scale(${pinch.zoom / getZoom()})`; // Vista previa barata; se repinta al soltar.
        }
    };

    // También con pointercancel: si el navegador se queda el gesto a medias, el zoom alcanzado se aplica igualmente.
    const up = e => {
        if (!pointers.delete(e.pointerId) || !pinch || pointers.size > 0) return;
        const zoom = pinch.zoom;
        pinch = null;
        column.style.transform = '';
        onZoom(zoom);
    };

    // La rueda sola desplaza (lo hace el navegador); con Ctrl amplía o reduce las páginas en vez de todo el documento.
    const wheel = e => {
        if (!e.ctrlKey || isBlocked() || e.deltaY === 0) return;
        e.preventDefault();
        if (wheelZoom === null) beginPreview();
        wheelZoom = clampZoom((wheelZoom ?? getZoom()) * (e.deltaY < 0 ? WHEEL_ZOOM_STEP : 1 / WHEEL_ZOOM_STEP));
        // Vista previa con CSS (a lo más una vez por frame); el relayout caro es uno solo al acabar la ráfaga.
        if (!wheelFrame) {
            wheelFrame = requestAnimationFrame(() => {
                wheelFrame = 0;
                column.style.transformOrigin = origin;
                column.style.transform = `scale(${wheelZoom / getZoom()})`;
            });
        }
        clearTimeout(wheelTimer);
        wheelTimer = setTimeout(commitWheel, WHEEL_SETTLE_MS);
    };

    const commitWheel = () => {
        cancelAnimationFrame(wheelFrame);
        wheelFrame = 0;
        wheelTimer = 0;
        const zoom = wheelZoom;
        wheelZoom = null;
        column.style.transform = '';
        if (zoom === null) return;
        if (isBlocked()) onZoomCancel(); // Empezó un recorte durante la ráfaga: no se hace zoom.
        else onZoom(zoom);
    };

    // Con dos dedos el navegador no debe reclamar el gesto como desplazamiento (mandaría pointercancel y el zoom se quedaría
    // casi en ×1); con uno sigue desplazando por touch-action. Es el patrón del visor de pdf.js.
    const twoFingers = e => {
        if (e.touches.length > 1) e.preventDefault();
    };

    const handlers = [['pointerdown', down], ['pointermove', move], ['pointerup', up], ['pointercancel', up]];
    handlers.forEach(([type, handler]) => host.addEventListener(type, handler));
    host.addEventListener('wheel', wheel, { passive: false });
    host.addEventListener('touchmove', twoFingers, { passive: false });
    return () => {
        handlers.forEach(([type, handler]) => host.removeEventListener(type, handler));
        host.removeEventListener('wheel', wheel);
        host.removeEventListener('touchmove', twoFingers);
        clearTimeout(wheelTimer);
        cancelAnimationFrame(wheelFrame);
        column.style.transform = '';
    };
}

function distance(pointers) {
    const [a, b] = [...pointers.values()];
    return b ? Math.hypot(a.x - b.x, a.y - b.y) : 0;
}
