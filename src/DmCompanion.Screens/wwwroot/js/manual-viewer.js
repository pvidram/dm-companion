// Visor de manuales en PDF (pdf.js). Módulo ES que carga ManualViewerInterop; un solo documento abierto a la vez.
// Lectura continua: una columna con un hueco por página; solo las cercanas a la vista tienen canvas.
import { GlobalWorkerOptions, getDocument } from '../lib/pdfjs/pdf.min.mjs';
import { attachGestures } from './manual-gestures.js';
import './manual-geometry.js';
import './manual-page-tracker.js';
import './manual-pdf-tasks.js';
import './manual-scroll.js';

const geometry = globalThis.manualGeometry;
const scroll = globalThis.manualScroll;
const { createPageTracker } = globalThis.manualPageTracker;
const { loadDocument, renderWithRetry } = globalThis.manualPdfTasks;
const asset = path => new URL(path, import.meta.url).href;
GlobalWorkerOptions.workerSrc = asset('../lib/pdfjs/pdf.worker.min.mjs');

const MIN_ZOOM = 0.5;
const MAX_ZOOM = 4;
// Tope de píxeles de los canvas de la ventana (se reparte entre sus páginas): por encima, los WebView de Android no pintan nada.
const MAX_CANVAS_PIXELS = 16777216;
const SNIPPET_CHARS = 40;
const MAX_SEARCH_HITS = 500;
const PAGE_GAP = 8;
const PAGE_NOTICE_DELAY_MS = 150;
const RESIZE_DELAY_MS = 150;
const FAILED_PAGE_TEXT = 'No se pudo mostrar esta página';

let state = null;
let searchGeneration = 0;

// Los errores se reducen a su nombre (InvalidPDFException…): el mensaje podría citar el PDF y acaba en el log de .NET.
function sanitized(error) {
    return new Error(error && error.name ? error.name : 'Error');
}

export async function open(host, url, page, callbacks) {
    await close();
    const task = getDocument({
        url,
        rangeChunkSize: 65536,
        disableAutoFetch: true,
        // true y no false: con streaming, pdf.js sigue bajando el fichero entero aunque el servidor admita rangos.
        disableStream: true,
        cMapUrl: asset('../lib/pdfjs/cmaps/'),
        cMapPacked: true,
        standardFontDataUrl: asset('../lib/pdfjs/standard_fonts/'),
        wasmUrl: asset('../lib/pdfjs/wasm/')
    });
    const current = {
        task, doc: null, host, callbacks, column: document.createElement('div'),
        slots: [], sizes: [], known: [], heights: [], tops: [], cssWidth: 1, zoom: 1,
        painted: new Map(), pages: createPageTracker(),
        page: 1, lastNotified: 0, pinned: null, pinnedTop: 0, noticeTimer: 0, windowPages: 1, frame: 0,
        crop: null, previewing: false, zoomAnchor: null, cleanup: []
    };
    state = current;
    try {
        current.doc = await loadDocument(task);
    } catch (error) {
        if (state === current) state = null;
        throw sanitized(error);
    }
    if (state !== current) {
        await task.destroy();
        return null; // Otro open() o un close() llegaron antes: no es un error.
    }
    try {
        await mount(current);
        if (state !== current) return null;
        const [outline, hasText] = await Promise.all([readOutline(current.doc), detectText(current)]);
        if (state !== current) return null;
        const start = clampPage(current, page);
        scrollToPage(current, start);
        observe(current);
        // Si la primera página no se puede pintar ni a menor escala, el manual no se puede abrir.
        if (!await paintedPage(current, start)) throw new Error('RenderFailed');
        if (state !== current) return null;
        return { pageCount: current.doc.numPages, outline, hasText };
    } catch (error) {
        if (state === current) await close();
        throw sanitized(error);
    }
}

function clampPage(current, page) {
    return Math.min(Math.max(1, Math.trunc(page) || 1), current.doc.numPages);
}

// Desplaza el host hasta la página, sin animación. false si no se pudo pintar ni tras el reintento a menor escala.
export async function showPage(page) {
    if (!state || !state.doc) return true;
    const current = state;
    finishCrop(current, null); // Cambiar de página termina un recorte pendiente: el rectángulo era de la otra.
    const number = clampPage(current, page);
    scrollToPage(current, number);
    return await paintedPage(current, number);
}

export async function setZoom(zoom) {
    if (!state || !state.doc) return;
    finishCrop(state, null);
    applyZoom(state, zoom, null);
}

// anchor: el ancla tomada al empezar un gesto (null: se toma ahora de la posición actual).
function applyZoom(current, zoom, anchor) {
    const next = clampZoom(zoom);
    if (next === current.zoom) {
        // Sin cambio de escala no hace falta repintar, pero la vista previa pudo recortar el scroll: se recoloca.
        if (anchor) {
            restoreAnchor(current, anchor);
            refresh(current);
        }
        return;
    }
    current.zoom = next;
    relayout(current, anchor);
}

// Ancla de la vista: página y fracción vertical en lo alto, y fracción horizontal del ancho de la columna a la izquierda.
function takeAnchor(current) {
    const { host, column } = current;
    const anchor = scroll.anchorAt(current.tops, current.heights, host.scrollTop, currentPageOf(current));
    anchor.x = scroll.horizontalFraction(host.scrollLeft, column.offsetLeft, current.cssWidth);
    return anchor;
}

function restoreAnchor(current, anchor) {
    const { host, column } = current;
    current.pinned = anchor.page;
    setScroll(current, scroll.scrollForAnchor(current.tops, current.heights, anchor));
    host.scrollLeft = scroll.scrollForHorizontal(column.offsetLeft, current.cssWidth, anchor.x);
}

// Al empezar la vista previa del zoom: se toma el ancla (antes de que el escalado recorte el scroll) y se ignoran los refresh.
function beginPreview(current) {
    if (current.previewing) return;
    current.zoomAnchor = takeAnchor(current);
    current.previewing = true;
}

function endPreview(current) {
    const anchor = current.zoomAnchor;
    current.previewing = false;
    current.zoomAnchor = null;
    return anchor;
}

function clampZoom(zoom) {
    return Math.min(Math.max(zoom, MIN_ZOOM), MAX_ZOOM);
}

export async function close() {
    searchGeneration++;
    const current = state;
    state = null;
    if (!current) return;
    finishCrop(current, null);
    current.cleanup.forEach(undo => undo());
    clearTimeout(current.noticeTimer);
    cancelAnimationFrame(current.frame);
    for (const number of [...current.painted.keys()]) evict(current, number);
    current.column.remove();
    await current.task.destroy();
}

// Columna con un hueco por página. Todos toman el tamaño de la página 1 hasta que cada página se carga y lo corrige.
async function mount(current) {
    const { host, column } = current;
    const first = await current.doc.getPage(1);
    const base = first.getViewport({ scale: 1 });
    current.pages.release(first);
    if (state !== current) return; // close() cayó en el await: sin columna que dejar huérfana (open lo comprueba).
    const count = current.doc.numPages;
    current.sizes = Array.from({ length: count }, () => ({ width: base.width, height: base.height }));
    current.known = Array.from({ length: count }, (_, i) => i === 0);
    column.className = 'manual-pages';
    for (let number = 1; number <= count; number++) {
        const slot = document.createElement('div');
        slot.className = 'manual-page-slot';
        slot.dataset.page = String(number);
        slot.style.marginBottom = `${PAGE_GAP}px`;
        column.appendChild(slot);
        current.slots.push(slot);
    }
    const style = getComputedStyle(host);
    if (style.position === 'static') host.style.position = 'relative';
    if (style.overflow === 'visible') host.style.overflow = 'auto';
    host.appendChild(column);
    applyLayout(current);
}

// Ancho de la columna y altura de todos los huecos a partir de los tamaños conocidos y del zoom.
function applyLayout(current) {
    current.cssWidth = Math.max(1, current.host.clientWidth * current.zoom);
    current.column.style.width = `${current.cssWidth}px`;
    current.heights = current.sizes.map(size => scroll.slotHeight(size.width, size.height, current.cssWidth));
    current.slots.forEach((slot, index) => { slot.style.height = `${current.heights[index]}px`; });
    current.tops = scroll.layout(current.heights, PAGE_GAP).tops;
}

// Listeners del host (scroll, visibilidad, gestos, tamaño). Se montan tras colocar la página inicial para no pintar la 1 de más.
function observe(current) {
    const { host } = current;
    const request = () => {
        if (current.frame) return;
        current.frame = requestAnimationFrame(() => {
            current.frame = 0;
            if (state === current) refresh(current);
        });
    };
    host.addEventListener('scroll', request, { passive: true });
    // Margen de una pantalla por arriba y por abajo: las páginas cercanas se pintan antes de llegar a ellas.
    const visibility = new IntersectionObserver(request, { root: host, rootMargin: '100% 0px 100% 0px' });
    current.slots.forEach(slot => visibility.observe(slot));
    current.cleanup.push(attachGestures(host, current.column, {
        isBlocked: () => current.crop !== null,
        getZoom: () => current.zoom,
        clampZoom,
        onZoomStart: () => beginPreview(current),
        onZoom: zoom => {
            if (state !== current) return;
            const anchor = endPreview(current);
            finishCrop(current, null);
            applyZoom(current, zoom, anchor);
        },
        onZoomCancel: () => {
            if (state !== current) return;
            const anchor = endPreview(current);
            if (anchor) restoreAnchor(current, anchor);
            refresh(current);
        }
    }));
    let resizeTimer = 0;
    let lastWidth = host.clientWidth;
    const resize = new ResizeObserver(() => {
        if (host.clientWidth === lastWidth) return;
        lastWidth = host.clientWidth;
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => { if (state === current) relayout(current); }, RESIZE_DELAY_MS);
    });
    resize.observe(host);
    current.cleanup.push(() => {
        host.removeEventListener('scroll', request);
        visibility.disconnect();
        resize.disconnect();
        clearTimeout(resizeTimer);
    });
}

// Cambia el scrollTop del host; si hay una página fijada, anota dónde queda para saber cuándo desplaza el usuario.
function setScroll(current, top) {
    if (Math.abs(top - current.host.scrollTop) >= 1) current.host.scrollTop = top; // Reasignar el mismo valor corta la inercia en Android.
    if (current.pinned !== null) current.pinnedTop = current.host.scrollTop;
}

function scrollToPage(current, number) {
    clearTimeout(current.noticeTimer);
    current.noticeTimer = 0;
    current.page = number;
    current.lastNotified = number; // C# ya conoce esta página: la pidió él o la restaura al abrir.
    current.pinned = number;
    setScroll(current, current.tops[number - 1]);
}

// Página que se está leyendo. Tras showPage, un zoom o un recorte vale la fijada hasta que el usuario desplaza.
function currentPageOf(current) {
    const { host } = current;
    if (current.previewing) return current.pinned ?? current.page; // El scroll recortado por la vista previa no es del usuario.
    if (current.pinned !== null && Math.abs(host.scrollTop - current.pinnedTop) > 1) current.pinned = null;
    return current.pinned ?? scroll.currentPage(current.tops, current.heights, host.scrollTop, host.clientHeight);
}

// Pinta la ventana de la página actual y avisa (con retardo) de que ha cambiado. Devuelve las páginas de la ventana.
function refresh(current) {
    if (current.previewing) return current.painted; // Ni se repinta ni se avisa a C# durante la vista previa del zoom.
    const page = currentPageOf(current);
    scheduleNotice(current, page);
    return updateWindow(current, page);
}

// Espera a que la página quede pintada: true si se pintó (o si otro pintado la sustituyó), false si falló también el reintento.
async function paintedPage(current, number) {
    const entry = refresh(current).get(number);
    return entry ? await entry.promise : true;
}

function scheduleNotice(current, page) {
    current.page = page;
    // Throttle con disparo final: no se rearma el temporizador, así el contador se mueve mientras se desplaza.
    if (current.noticeTimer || page === current.lastNotified) return;
    current.noticeTimer = setTimeout(() => flushNotice(current), PAGE_NOTICE_DELAY_MS);
}

function flushNotice(current) {
    current.noticeTimer = 0;
    if (state !== current || current.page === current.lastNotified) return;
    const previous = current.lastNotified;
    current.lastNotified = current.page;
    current.callbacks.invokeMethodAsync('onPageChanged', current.page).catch(error => {
        if (state === current && current.lastNotified === current.page) current.lastNotified = previous; // Se reintentará en el próximo refresh.
        console.warn('onPageChanged falló:', error && error.name); // Solo el tipo: el mensaje podría citar contenido.
    });
}

function updateWindow(current, page) {
    const { host } = current;
    const range = scroll.paintWindow(current.tops, current.heights, host.scrollTop, host.clientHeight, host.clientHeight, page);
    for (const number of [...current.painted.keys()]) {
        if (number < range.first || number > range.last) evict(current, number);
    }
    current.windowPages = Math.max(1, range.last - range.first + 1);
    for (let number = range.first; number <= range.last; number++) {
        if (!current.painted.has(number)) startPaint(current, number);
    }
    return current.painted;
}

function startPaint(current, number) {
    const entry = { number, page: null, canvas: null, task: null, cancelled: false, promise: null };
    current.painted.set(number, entry);
    entry.promise = paintEntry(current, entry);
    return entry;
}

// true si se pintó (o si se canceló: desplazarse no es un error); false si falló también el reintento a la mitad de escala.
async function paintEntry(current, entry) {
    let page;
    try {
        page = await current.doc.getPage(entry.number);
    } catch (error) {
        if (entry.cancelled || state !== current) return true;
        showFailure(current, entry, error);
        return false;
    }
    if (entry.cancelled || state !== current) {
        current.pages.release(page);
        return true;
    }
    entry.page = page;
    current.pages.add(page);
    const base = page.getViewport({ scale: 1 });
    correctSlot(current, entry.number, base.width, base.height);
    let failure = null;
    const painted = await renderWithRetry(async factor => {
        if (entry.cancelled || state !== current) return;
        try {
            await paintCanvas(current, entry, page, base, factor);
        } catch (error) {
            failure = error;
            throw error;
        }
    });
    if (!painted && !entry.cancelled && state === current) showFailure(current, entry, failure);
    return painted || entry.cancelled;
}

async function paintCanvas(current, entry, page, base, factor) {
    const scale = current.cssWidth / base.width;
    const cssPixels = current.cssWidth * base.height * scale;
    const ratio = Math.min(window.devicePixelRatio || 1, Math.sqrt(scroll.pixelBudget(MAX_CANVAS_PIXELS, current.windowPages) / cssPixels));
    const viewport = page.getViewport({ scale: scale * ratio * factor });
    if (!entry.canvas) {
        entry.canvas = document.createElement('canvas');
        current.slots[entry.number - 1].appendChild(entry.canvas);
    }
    const { canvas } = entry;
    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);
    const task = page.render({ canvasContext: canvas.getContext('2d'), viewport });
    entry.task = task;
    try {
        await task.promise;
    } finally {
        if (entry.task === task) entry.task = null;
    }
}

// El hueco muestra el aviso y el resto del manual sigue funcionando; al .NET log solo va el tipo de error.
function showFailure(current, entry, error) {
    const slot = current.slots[entry.number - 1];
    if (entry.canvas) {
        entry.canvas.width = 0;
        entry.canvas.height = 0;
        entry.canvas.remove();
        entry.canvas = null;
    }
    slot.classList.add('manual-page-slot--failed');
    slot.textContent = FAILED_PAGE_TEXT;
    current.callbacks.invokeMethodAsync('onPageFailed', error && error.name ? error.name : 'Error').catch(() => {
        // Aviso informativo: si C# no lo atiende, el hueco ya muestra el mensaje al usuario.
    });
}

// La página sale de la ventana: se cancela su pintado, se vacía y quita su canvas y se libera la página de pdf.js.
function evict(current, number) {
    const entry = current.painted.get(number);
    if (!entry) return;
    current.painted.delete(number);
    entry.cancelled = true;
    if (entry.task) entry.task.cancel();
    if (entry.canvas) {
        entry.canvas.width = 0;
        entry.canvas.height = 0;
    }
    const slot = current.slots[number - 1];
    slot.replaceChildren();
    slot.classList.remove('manual-page-slot--failed');
    if (entry.page) current.pages.remove(entry.page);
}

// Una página con otro tamaño que el supuesto corrige su hueco, y las que aún no se han leído toman ese tamaño como estimación
// (así, tras un salto a la 250, al subir no corrige cada página por separado y se corta la inercia). El scroll se compensa.
function correctSlot(current, number, width, height) {
    const index = number - 1;
    const read = { width, height };
    current.known[index] = true;
    current.sizes[index] = read;
    current.sizes = scroll.fillUnknownSizes(current.sizes, current.known, read);
    const next = current.sizes.map(size => scroll.slotHeight(size.width, size.height, current.cssWidth));
    if (next.every((h, i) => h === current.heights[i])) return;
    const oldTops = current.tops;
    const oldHeights = current.heights;
    current.heights = next;
    current.tops = scroll.layout(next, PAGE_GAP).tops;
    next.forEach((h, i) => { if (h !== oldHeights[i]) current.slots[i].style.height = `${h}px`; });
    setScroll(current, scroll.remapScroll(oldTops, oldHeights, current.tops, next, current.host.scrollTop));
    if (current.crop) placeCropOverlay(current);
}

// Zoom o cambio de ancho del host: recalcula los huecos conservando la página actual y la fracción que estaba en lo alto.
function relayout(current, taken) {
    const anchor = taken ?? takeAnchor(current);
    for (const number of [...current.painted.keys()]) evict(current, number);
    applyLayout(current);
    current.pinned = anchor.page;
    restoreAnchor(current, anchor);
    if (current.crop) placeCropOverlay(current);
    refresh(current);
}

async function readOutline(doc) {
    const items = await doc.getOutline().catch(() => null);
    const flat = [];
    const walk = async (nodes, level) => {
        for (const node of nodes || []) {
            flat.push({ title: node.title || '', page: await resolvePage(doc, node.dest), level });
            await walk(node.items, level + 1);
        }
    };
    await walk(items, 0);
    return flat;
}

async function resolvePage(doc, dest) {
    try {
        const explicit = typeof dest === 'string' ? await doc.getDestination(dest) : dest;
        if (!Array.isArray(explicit) || explicit[0] == null) return null;
        const target = explicit[0];
        const index = Number.isInteger(target) ? target : await doc.getPageIndex(target);
        return index + 1;
    } catch {
        return null; // Destino roto: la entrada se muestra sin enlace.
    }
}

async function pageText(current, number) {
    const page = await current.doc.getPage(number);
    try {
        const content = await page.getTextContent();
        return content.items.map(item => (item.str || '') + (item.hasEOL ? ' ' : '')).join('');
    } finally {
        current.pages.release(page);
    }
}

async function detectText(current) {
    for (let number = 1; number <= Math.min(3, current.doc.numPages); number++) {
        const text = await pageText(current, number).catch(() => '');
        if (text.trim()) return true;
    }
    return false;
}

// Texto sin tildes y en minúsculas, con el índice del original de cada carácter para recortar el fragmento.
function normalize(text) {
    let chars = '';
    const origin = [];
    for (let i = 0; i < text.length; i++) {
        const folded = text[i].normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
        for (const c of folded) { chars += c; origin.push(i); }
    }
    return { chars, origin };
}

function snippet(text, start, end) {
    const from = Math.max(0, start - SNIPPET_CHARS);
    const to = Math.min(text.length, end + SNIPPET_CHARS);
    const body = text.slice(from, to).replace(/\s+/g, ' ').trim();
    return (from > 0 ? '…' : '') + body + (to < text.length ? '…' : '');
}

export async function search(text, session) {
    const generation = ++searchGeneration;
    const current = state;
    const needle = normalize(text || '').chars.trim();
    if (!current || !current.doc || !needle) return true;
    const cancelled = async () => generation !== searchGeneration || state !== current || await session.invokeMethodAsync('isCancelled');
    let hits = 0;
    for (let number = 1; number <= current.doc.numPages; number++) {
        if (await cancelled()) return false;
        const original = await pageText(current, number).catch(() => '');
        const { chars, origin } = normalize(original);
        for (let at = chars.indexOf(needle); at >= 0; at = chars.indexOf(needle, at + needle.length)) {
            if (await cancelled()) return false;
            const end = origin[at + needle.length - 1] + 1;
            await session.invokeMethodAsync('onHit', number, snippet(original, origin[at], end));
            if (++hits >= MAX_SEARCH_HITS) return true; // Tope: una palabra muy común no inunda el interop.
        }
    }
    return !(generation !== searchGeneration || state !== current);
}

// PNG de la página o del recorte a la escala que llena maxWidth × maxHeight. Se devuelve como bytes (IJSStreamReference en .NET).
export async function renderPng(page, crop, maxWidth, maxHeight) {
    if (!state || !state.doc) throw new Error('NoDocument');
    const current = state;
    const pdfPage = await current.doc.getPage(clampPage(current, page));
    const base = pdfPage.getViewport({ scale: 1 });
    const area = crop ? geometry.cropToPdf(crop, base.width, base.height) : { x: 0, y: 0, width: base.width, height: base.height };
    if (!(area.width > 0) || !(area.height > 0)) {
        current.pages.release(pdfPage);
        throw new Error('EmptyCrop');
    }
    const scale = geometry.fitScale(area.width, area.height, maxWidth, maxHeight);
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(area.width * scale));
    canvas.height = Math.max(1, Math.round(area.height * scale));
    try {
        await pdfPage.render({
            canvasContext: canvas.getContext('2d'),
            viewport: pdfPage.getViewport({ scale }),
            transform: [1, 0, 0, 1, -area.x * scale, -area.y * scale]
        }).promise;
        const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        if (!blob) throw new Error('EncodeFailed');
        return new Uint8Array(await blob.arrayBuffer());
    } catch (error) {
        throw sanitized(error);
    } finally {
        canvas.width = 0;
        canvas.height = 0;
        if (state === current) current.pages.release(pdfPage);
    }
}

// Modo recorte sobre el hueco de la página actual, con el desplazamiento del host bloqueado mientras dura.
export function pickCrop() {
    if (!state || !state.doc) return Promise.resolve(null);
    const current = state;
    finishCrop(current, null);
    const page = currentPageOf(current);
    // Se fija la página y se avisa ya a C#: «Enviar» debe usar la misma página que se recorta, sin esperar al retardo.
    current.pinned = page;
    current.pinnedTop = current.host.scrollTop;
    current.page = page;
    flushNotice(current);
    const overlay = document.createElement('div');
    overlay.className = 'manual-crop';
    const box = document.createElement('div');
    box.className = 'manual-crop-rect';
    overlay.appendChild(box);
    Object.assign(overlay.style, { position: 'absolute', touchAction: 'none', cursor: 'crosshair' });
    Object.assign(box.style, { position: 'absolute', display: 'none' });
    const restoreOverflow = current.host.style.overflow;
    current.host.style.overflow = 'hidden';
    current.host.appendChild(overlay);
    return new Promise(resolve => {
        const crop = { overlay, box, resolve, start: null, rect: null, page, restoreOverflow };
        current.crop = crop;
        placeCropOverlay(current);
        const point = e => { const r = overlay.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top, w: r.width, h: r.height }; };
        overlay.addEventListener('pointerdown', e => { overlay.setPointerCapture(e.pointerId); crop.start = point(e); });
        overlay.addEventListener('pointermove', e => {
            if (!crop.start) return;
            const p = point(e);
            crop.rect = geometry.rectFromDrag(crop.start.x, crop.start.y, p.x, p.y, p.w, p.h);
            Object.assign(box.style, {
                display: 'block', left: `${crop.rect.x * 100}%`, top: `${crop.rect.y * 100}%`,
                width: `${crop.rect.width * 100}%`, height: `${crop.rect.height * 100}%`
            });
        });
        overlay.addEventListener('pointerup', () => { crop.start = null; });
    });
}

// El velo cubre exactamente el hueco de la página recortada; el rectángulo va en fracciones de la página, así que sigue en su sitio.
function placeCropOverlay(current) {
    const slot = current.slots[current.crop.page - 1];
    Object.assign(current.crop.overlay.style, {
        left: `${slot.offsetLeft}px`, top: `${slot.offsetTop}px`, width: `${slot.offsetWidth}px`, height: `${slot.offsetHeight}px`
    });
}

export function confirmCrop() {
    if (!state || !state.crop) return;
    const rect = state.crop.rect;
    finishCrop(state, rect && rect.width > 0 && rect.height > 0 ? rect : null);
}

export function cancelCrop() {
    if (state) finishCrop(state, null);
}

function finishCrop(current, rect) {
    const crop = current.crop;
    if (!crop) return;
    current.crop = null;
    crop.overlay.remove();
    current.host.style.overflow = crop.restoreOverflow;
    crop.resolve(rect);
}
