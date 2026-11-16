// Visor de manuales en PDF (pdf.js). Módulo ES que carga ManualViewerInterop; un solo documento abierto a la vez.
import { GlobalWorkerOptions, getDocument } from '../lib/pdfjs/pdf.min.mjs';
import { attachGestures } from './manual-gestures.js';
import './manual-geometry.js';
import './manual-page-tracker.js';
import './manual-pdf-tasks.js';

const geometry = globalThis.manualGeometry;
const { createPageTracker } = globalThis.manualPageTracker;
const { loadDocument, renderWithRetry } = globalThis.manualPdfTasks;
const asset = path => new URL(path, import.meta.url).href;
GlobalWorkerOptions.workerSrc = asset('../lib/pdfjs/pdf.worker.min.mjs');

const MIN_ZOOM = 0.5;
const MAX_ZOOM = 4;
// Tope de píxeles del canvas en pantalla: por encima, los WebView de Android no pintan nada.
const MAX_CANVAS_PIXELS = 16777216;
const SNIPPET_CHARS = 40;
const MAX_SEARCH_HITS = 500;

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
    const current = { task, doc: null, host, callbacks, canvas: document.createElement('canvas'), page: 1, zoom: 1, renderTask: null, renderGeneration: 0, crop: null, cleanup: [], pages: createPageTracker() };
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
        mount(current);
        const [outline, hasText] = await Promise.all([readOutline(current.doc), detectText(current)]);
        if (state !== current) return null;
        current.page = clampPage(current, page);
        // Si la primera página no se puede pintar ni a menor escala, el manual no se puede abrir.
        if (!await render(current)) throw new Error('RenderFailed');
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

// false si la página no se pudo pintar ni tras el reintento a menor escala.
export async function showPage(page) {
    if (!state || !state.doc) return true;
    finishCrop(state, null); // Cambiar de página termina un recorte pendiente: el rectángulo era de la otra.
    state.page = clampPage(state, page);
    return await render(state);
}

export async function setZoom(zoom) {
    if (!state || !state.doc) return;
    finishCrop(state, null);
    state.zoom = clampZoom(zoom);
    await render(state);
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
    if (current.renderTask) current.renderTask.cancel();
    current.canvas.width = 0;
    current.canvas.height = 0;
    current.canvas.remove();
    await current.task.destroy();
}

function mount(current) {
    const { host, canvas } = current;
    canvas.className = 'manual-page';
    canvas.style.touchAction = 'none';
    canvas.style.display = 'block';
    host.appendChild(canvas);
    const style = getComputedStyle(host);
    if (style.position === 'static') host.style.position = 'relative';
    if (style.overflow === 'visible') host.style.overflow = 'auto';
    current.cleanup.push(attachGestures(canvas, host, {
        isBlocked: () => current.crop !== null,
        getZoom: () => current.zoom,
        clampZoom,
        onZoom: zoom => { if (state === current) setZoom(zoom).catch(() => {}); },
        onSwipe: direction => current.callbacks.invokeMethodAsync('onSwipe', direction).catch(() => {})
    }));
    let resizeTimer = 0;
    let lastWidth = host.clientWidth;
    const observer = new ResizeObserver(() => {
        if (host.clientWidth === lastWidth) return;
        lastWidth = host.clientWidth;
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => { if (state === current) render(current).catch(() => {}); }, 150);
    });
    observer.observe(host);
    current.cleanup.push(() => { observer.disconnect(); clearTimeout(resizeTimer); });
}

// true si se pintó (o si otro pintado lo sustituyó); false si falló también el reintento a la mitad de escala.
async function render(current) {
    const generation = ++current.renderGeneration;
    const page = await current.doc.getPage(current.page);
    if (state !== current) return true;
    if (generation !== current.renderGeneration) {
        current.pages.release(page);
        return true;
    }
    if (current.renderTask) current.renderTask.cancel();
    current.pages.show(page);
    const base = page.getViewport({ scale: 1 });
    const cssWidth = Math.max(1, current.host.clientWidth * current.zoom);
    const scale = cssWidth / base.width;
    const cssPixels = cssWidth * base.height * scale;
    const ratio = Math.min(window.devicePixelRatio || 1, Math.sqrt(MAX_CANVAS_PIXELS / cssPixels));
    const { canvas } = current;
    canvas.style.width = `${cssWidth}px`;
    canvas.style.height = `${base.height * scale}px`;
    // Un redimensionado del host repinta sin cancelar el recorte: el velo se recoloca sobre la página.
    if (current.crop) placeCropOverlay(current);
    return renderWithRetry(async factor => {
        if (state !== current || generation !== current.renderGeneration) return;
        await paint(current, page, scale * ratio * factor);
    });
}

async function paint(current, page, scale) {
    const viewport = page.getViewport({ scale });
    const { canvas } = current;
    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);
    const task = page.render({ canvasContext: canvas.getContext('2d'), viewport });
    current.renderTask = task;
    try {
        await task.promise;
    } finally {
        if (current.renderTask === task) current.renderTask = null;
    }
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

export function pickCrop() {
    if (!state || !state.doc) return Promise.resolve(null);
    const current = state;
    finishCrop(current, null);
    const overlay = document.createElement('div');
    overlay.className = 'manual-crop';
    const box = document.createElement('div');
    box.className = 'manual-crop-rect';
    overlay.appendChild(box);
    Object.assign(overlay.style, { position: 'absolute', touchAction: 'none', cursor: 'crosshair' });
    Object.assign(box.style, { position: 'absolute', display: 'none' });
    current.host.appendChild(overlay);
    return new Promise(resolve => {
        const crop = { overlay, box, resolve, start: null, rect: null };
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

// El velo cubre exactamente el canvas; el rectángulo va en fracciones de la página, así que sigue en su sitio.
function placeCropOverlay(current) {
    const { canvas } = current;
    Object.assign(current.crop.overlay.style, {
        left: `${canvas.offsetLeft}px`, top: `${canvas.offsetTop}px`, width: canvas.style.width, height: canvas.style.height
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
    crop.resolve(rect);
}
