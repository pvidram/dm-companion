// Modo Niebla del lienzo de la tablet (map-canvas.js): capa de niebla, borrador de Trazar y toques del lápiz.
// C# manda: el JS solo avisa (zona añadida, alternada o borrada; esquinas del borrador) y repinta lo que recibe.
// La geometría está en fog-geometry.js.
(function (root) {
    'use strict';

    // Radio (px de pantalla) en el que un toque cerca de la primera esquina cierra la zona, a cualquier zoom.
    const FOG_CLOSE_RADIUS_PX = 24;
    const REVEALED_DASH = [12, 8];
    const DRAFT_LINE_PX = 2;
    const DRAFT_DOT_PX = 6;

    function ignore() { /* la llamada a C# puede fallar si el circuito se cerró */ }

    function notify(inst, method, ...args) {
        inst.dotnet.invokeMethodAsync(method, ...args).catch(ignore);
    }

    function colors(host) {
        const css = getComputedStyle(host.closest('.ds-root') || document.documentElement);
        const get = (name, fallback) => css.getPropertyValue(name).trim() || fallback;
        return { fog: get('--fog-dm', 'rgba(43, 33, 24, 0.45)'), accent: get('--accent-on-wood', '#C9A24A') };
    }

    // Tapadas: velo semitransparente encima de los tokens. Destapadas: contorno discontinuo, solo en modo Niebla.
    // La capa nunca escucha: los tokens bajo niebla se siguen tocando en Navegar.
    function draw(inst) {
        const layer = inst.layers.fog;
        layer.destroyChildren();
        const palette = colors(inst.host);
        for (const zone of inst.dto.fog || []) {
            const base = { points: zone.points, closed: true, listening: false, perfectDrawEnabled: false };
            if (!zone.revealed) layer.add(new Konva.Line({ ...base, fill: palette.fog }));
            else if (inst.fogActive) {
                layer.add(new Konva.Line({ ...base, stroke: palette.accent, strokeWidth: 2, dash: REVEALED_DASH.slice(), strokeScaleEnabled: false }));
            }
        }
        layer.batchDraw();
    }

    function draftOf(inst) {
        if (!inst.fogDraft) inst.fogDraft = { corners: [], node: null };
        return inst.fogDraft;
    }

    // Polígono en curso: lados, línea de cierre provisional (de la última esquina a la primera) y puntos.
    // Grosores en px de pantalla: se dividen por la escala en cada pintado.
    function draftNode(corners, color) {
        return new Konva.Shape({
            listening: false,
            sceneFunc(context, shape) {
                const scale = shape.getAbsoluteScale().x || 1;
                context.setAttr('strokeStyle', color);
                context.setAttr('fillStyle', color);
                context.setAttr('lineWidth', DRAFT_LINE_PX / scale);
                context.beginPath();
                corners.forEach((c, i) => (i === 0 ? context.moveTo(c.x, c.y) : context.lineTo(c.x, c.y)));
                context.stroke();
                if (corners.length >= 2) {
                    const [first, last] = [corners[0], corners[corners.length - 1]];
                    context.setLineDash(REVEALED_DASH.map(v => v / scale));
                    context.beginPath();
                    context.moveTo(last.x, last.y);
                    context.lineTo(first.x, first.y);
                    context.stroke();
                    context.setLineDash([]);
                }
                for (const c of corners) {
                    context.beginPath();
                    context.arc(c.x, c.y, DRAFT_DOT_PX / scale, 0, Math.PI * 2);
                    context.fill();
                }
            },
        });
    }

    function redrawDraft(inst) {
        const draft = draftOf(inst);
        if (draft.node) draft.node.destroy();
        draft.node = draft.corners.length > 0 ? draftNode(draft.corners, colors(inst.host).accent) : null;
        if (draft.node) inst.layers.draft.add(draft.node);
        inst.layers.draft.batchDraw();
        notify(inst, 'OnFogDraftChanged', draft.corners.length);
    }

    // Descarta el borrador (cambio de modo, de herramienta o de mapa, «Cancelar»). Sin borrador no avisa.
    function clearDraft(inst) {
        const draft = draftOf(inst);
        if (draft.corners.length === 0) return;
        draft.corners = [];
        redrawDraft(inst);
    }

    function undoCorner(inst) {
        const draft = draftOf(inst);
        if (draft.corners.length === 0) return;
        draft.corners.pop();
        redrawDraft(inst);
    }

    function trace(inst, point) {
        const draft = draftOf(inst);
        const geometry = root.fogGeometry;
        const closing = draft.corners.length > 0
            && geometry.closesAt(draft.corners[0], point, inst.stage.scaleX(), FOG_CLOSE_RADIUS_PX);
        if (closing && draft.corners.length >= 3) {
            const points = draft.corners.flatMap(c => [c.x, c.y]);
            draft.corners = [];
            redrawDraft(inst);
            notify(inst, 'OnFogZoneAdded', points);
            return;
        }
        // Con menos de 3 esquinas, un toque junto a la primera no añade otra encima: daría una zona sin área.
        if (closing) return;
        draft.corners.push({ x: point.x, y: point.y });
        redrawDraft(inst);
    }

    // Toque del lápiz (o del ratón en el PC) en coordenadas del mapa, según la herramienta elegida.
    function onPenDown(inst, worldPoint) {
        const tool = inst.fogTool || 'Reveal';
        if (tool === 'Trace') { trace(inst, worldPoint); return; }
        const zone = root.fogGeometry.zoneAt(inst.dto.fog, worldPoint.x, worldPoint.y);
        if (!zone) return;
        notify(inst, tool === 'Erase' ? 'OnFogZoneErased' : 'OnFogZoneToggled', zone.id);
    }

    const api = { draw, onPenDown, clearDraft, undoCorner, FOG_CLOSE_RADIUS_PX };
    root.fogMode = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
