// Modo Niebla del lienzo de la tablet (map-canvas.js): capa de niebla, borrador de Trazar, gesto de Mano alzada y toques del lápiz.
// Herramientas (inst.fogTool): 'Reveal', 'Trace', 'Erase', 'Freehand' (gesto, ver freehandGesture) y 'Retype' (cambiar tipo).
// C# manda: el JS solo avisa (zona añadida, alternada, borrada o cambiada de tipo; esquinas del borrador) y repinta lo que recibe.
// La geometría está en fog-geometry.js.
(function (root) {
    'use strict';

    // Radio (px de pantalla) en el que un toque cerca de la primera esquina cierra la zona, a cualquier zoom.
    const FOG_CLOSE_RADIUS_PX = 24;
    const REVEALED_DASH = [12, 8];
    const DRAFT_LINE_PX = 2;
    const DRAFT_DOT_PX = 6;
    // Mano alzada: tolerancia del simplificado y separación mínima entre puntos recogidos (px de pantalla).
    const FREEHAND_TOLERANCE_PX = 4;
    const FREEHAND_MIN_SPACING_PX = 2;

    function ignore() { /* la llamada a C# puede fallar si el circuito se cerró */ }

    function notify(inst, method, ...args) {
        inst.dotnet.invokeMethodAsync(method, ...args).catch(ignore);
    }

    function colors(host) {
        const css = getComputedStyle(host.closest('.ds-root') || document.documentElement);
        const get = (name, fallback) => css.getPropertyValue(name).trim() || fallback;
        return { fog: get('--fog-dm', 'rgba(43, 33, 24, 0.45)'), npc: get('--fog-npc-dm', 'rgba(74, 52, 110, 0.45)'), accent: get('--accent-on-wood', '#C9A24A') };
    }

    function fillFor(zone, palette) {
        return zone.type === 'npc' ? palette.npc : palette.fog;
    }

    // Tapadas: velo semitransparente encima de los tokens. Destapadas: contorno discontinuo, solo en modo Niebla.
    // La capa nunca escucha: los tokens bajo niebla se siguen tocando en Navegar.
    function draw(inst) {
        const layer = inst.layers.fog;
        layer.destroyChildren();
        const palette = colors(inst.host);
        for (const zone of inst.dto.fog || []) {
            const base = { points: zone.points, closed: true, listening: false, perfectDrawEnabled: false };
            if (!zone.revealed) layer.add(new Konva.Line({ ...base, fill: fillFor(zone, palette) }));
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
        if (tool === 'Freehand') return;
        if (tool === 'Trace') { trace(inst, worldPoint); return; }
        const zone = root.fogGeometry.zoneAt(inst.dto.fog, worldPoint.x, worldPoint.y);
        if (!zone) return;
        const method = { Erase: 'OnFogZoneErased', Retype: 'OnFogZoneRetyped' }[tool] || 'OnFogZoneToggled';
        notify(inst, method, zone.id);
    }

    // Mano alzada: añade el punto salvo que quede a menos de FREEHAND_MIN_SPACING_PX de pantalla del anterior.
    // No recorta al mapa: un trazo que sale de él se entrega tal cual y la validez la decide Core.
    function addFreehandPoint(gesture, point, scale) {
        const last = gesture.pts[gesture.pts.length - 1];
        if (last && Math.hypot(point.x - last.x, point.y - last.y) * scale < FREEHAND_MIN_SPACING_PX) return;
        gesture.pts.push({ x: point.x, y: point.y });
    }

    function flat(pts) { return pts.flatMap(p => [p.x, p.y]); }

    function clearFreehandDraft(inst) {
        inst.layers.draft.destroyChildren();
        inst.layers.draft.batchDraw();
    }

    // Trazo a mano alzada: contorno mientras dura el gesto, más una línea discontinua provisional de cierre.
    // Al soltar se simplifica y se cierra; el cierre sigue llegando si el lápiz sale del lienzo (captura del puntero).
    const freehandGesture = {
        start(inst, gesture, event) {
            gesture.pts = [];
            const color = colors(inst.host).accent;
            const line = extra => new Konva.Line({ stroke: color, strokeWidth: DRAFT_LINE_PX, strokeScaleEnabled: false, listening: false, ...extra });
            gesture.outline = line({ points: [], lineCap: 'round', lineJoin: 'round' });
            gesture.closing = line({ points: [], dash: REVEALED_DASH.slice() });
            inst.layers.draft.add(gesture.outline, gesture.closing);
            freehandGesture.move(inst, gesture, event);
        },
        move(inst, gesture, event) {
            const core = root.mapCore;
            const scale = inst.stage.scaleX();
            for (const e of core.eventPoints(event)) addFreehandPoint(gesture, core.worldPoint(inst, core.containerPoint(inst, e)), scale);
            const pts = gesture.pts;
            gesture.outline.points(flat(pts));
            gesture.closing.points(pts.length > 1 ? [pts[pts.length - 1].x, pts[pts.length - 1].y, pts[0].x, pts[0].y] : []);
            inst.layers.draft.batchDraw();
        },
        end(inst, gesture) {
            const points = root.fogGeometry.closeFreehand(gesture.pts, FREEHAND_TOLERANCE_PX / inst.stage.scaleX());
            clearFreehandDraft(inst);
            if (points) notify(inst, 'OnFogZoneAdded', points);
        },
        cancel(inst) { clearFreehandDraft(inst); },
    };

    // Cambiar de herramienta descarta la zona en curso y, si hay un trazo a mano alzada sin soltar, cancela el gesto.
    function setTool(inst, name, cancelGesture) {
        if (inst.gesture && inst.gesture.kind === 'fogFreehand') cancelGesture(inst);
        inst.fogTool = name;
        clearDraft(inst);
    }

    const api = {
        draw, onPenDown, clearDraft, setTool, undoCorner, fillFor, addFreehandPoint, freehandGesture,
        FOG_CLOSE_RADIUS_PX, FREEHAND_TOLERANCE_PX, FREEHAND_MIN_SPACING_PX,
    };
    root.fogMode = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
