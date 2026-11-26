// Lienzo del mapa (Konva). Un único mapa a la vez: window.mapCanvas.{load, render, setMode, setFogTool, fogDraft, fit, viewCenter, viewRect, hostRect, setTvFrame, beginGridAdjust, beginGridCalibrate, setCalibrateCells, endGridAdjust, dispose}.
// Capas, de abajo arriba: imagen o lienzo, cuadrícula, trazos, tokens, niebla (fog-mode.js), encuadre de la TV (tv-frame.js) y borrador (trazo, medición o zona en curso).
// El estado manda C#: JS pinta lo que recibe y solo informa de gestos (token movido o tocado, imagen cargada,
// trazo añadido o borrado). La medición no cruza el puente.
// Orquesta los módulos map-*.js, que se cargan antes (ver los index de cada host): este fichero solo expone la API que llama C#.
(function () {
    'use strict';

    const core = window.mapCore;
    const { theme, containerPoint, worldPoint } = core;
    const { tryFit } = window.mapView;
    const { drawGrid, drawAll } = window.mapLayers;
    const { setCanvasSize, backgroundKey, loadBackground } = window.mapBackground;
    const { modeKey } = window.mapMode;
    const { bindGestures } = window.mapTouch;
    const { cancelGesture } = window.mapGestures;
    const { bindInput, applyMode } = window.mapInput;

    // Sin esto Konva ignora el segundo dedo mientras se arrastra, y el pellizco no funciona.
    Konva.hitOnDragEnabled = true;

    function dispose() {
        const inst = core.current();
        if (!inst) return;
        core.setCurrent(null);
        inst.bgTicket++;
        cancelGesture(inst);
        inst.abort.abort();
        inst.host.style.cursor = '';
        inst.observer.disconnect();
        inst.stage.destroy();
    }

    function load(host, dto, dotnet) {
        dispose();
        const stage = new Konva.Stage({
            container: host,
            width: host.clientWidth,
            height: host.clientHeight,
            draggable: true,
        });
        const layers = {
            background: new Konva.Layer({ listening: false }),
            grid: new Konva.Layer({ listening: false }),
            strokes: new Konva.Layer({ listening: false }),
            tokens: new Konva.Layer(),
            fog: new Konva.Layer({ listening: false }),
            tvFrame: new Konva.Layer({ listening: false }),
            draft: new Konva.Layer({ listening: false }),
        };
        for (const layer of Object.values(layers)) stage.add(layer);

        const inst = {
            host, dotnet, stage, layers, dto,
            key: backgroundKey(dto),
            mode: 'Navigate', tool: null,
            mapW: 0, mapH: 0, fitScale: 0, needsFit: true,
            bgImage: null, bgTicket: 0, pinching: false,
            adjust: null, // ajuste de la cuadrícula con los dedos en curso (grid-adjust-mode.js), o null
            portraits: new Map(),
            // Dibujo, goma y medición: gesto en curso (o null), dedos apoyados, último evento del lápiz y trazos ya borrados.
            gesture: null, touches: new Set(), lastPenAt: -Infinity, erased: new Set(),
            abort: new AbortController(), appliedKey: 'Navigate|',
            fogTool: 'Reveal', fogActive: false, fogDraft: null, // modo Niebla (fog-mode.js)
        };
        inst.observer = new ResizeObserver(() => {
            const width = host.clientWidth;
            const height = host.clientHeight;
            if (width > 0 && height > 0) stage.size({ width, height });
            tryFit(inst);
        });
        inst.observer.observe(host);
        core.setCurrent(inst);
        bindGestures(inst);
        bindInput(inst);
        loadBackground(inst);
    }

    function render(dto) {
        const inst = core.current();
        if (!inst) return;
        inst.dto = dto;
        const key = backgroundKey(dto);
        if (key !== inst.key) {
            inst.key = key;
            window.fogMode.clearDraft(inst); // otro mapa: la zona en curso no vale
            loadBackground(inst);
            return;
        }
        // Lienzo en blanco: el tamaño de C# puede cambiar sin cambiar de mapa (p. ej. SetImageSize tras un fallo).
        if (!dto.imageUrl) setCanvasSize(inst);
        drawAll(inst);
        // Pasar a solo lectura (u otro cambio que altere el modo efectivo) reajusta las capas; si no, no se
        // toca nada: este repintado llega también tras cada trazo y no debe cortar el siguiente.
        if (modeKey(inst) !== inst.appliedKey) applyMode(inst);
    }

    function setMode(mode, tool) {
        if (!core.current()) return;
        core.current().mode = mode;
        core.current().tool = tool;
        applyMode(core.current());
    }

    // Herramienta del modo Niebla: 'Reveal', 'Trace', 'Erase', 'Freehand' o 'Retype'. Cambiarla descarta la zona en curso.
    function setFogTool(name) {
        if (!core.current()) return;
        window.fogMode.setTool(core.current(), name, cancelGesture);
    }

    // Botones «Deshacer esquina» ('undo') y «Cancelar» ('cancel') de Trazar.
    function fogDraft(action) {
        if (!core.current()) return;
        if (action === 'undo') window.fogMode.undoCorner(core.current());
        else if (action === 'cancel') window.fogMode.clearDraft(core.current());
    }

    // Entra en el modo de ajuste con la cuadrícula del último estado de C#. `minCell` y `maxCell` son los límites de los campos numéricos.
    function beginGridAdjust(minCell, maxCell) {
        const inst = core.current();
        if (!inst || inst.adjust || !inst.dto.grid) return false;
        const { cellSize, offsetX, offsetY } = inst.dto.grid;
        inst.adjust = window.gridAdjustMode.create({
            grid: { cellSize, offsetX, offsetY },
            limits: { min: minCell, max: maxCell },
            toMap: point => worldPoint(inst, containerPoint(inst, { clientX: point.x, clientY: point.y })),
            onChange: () => drawGrid(inst),
        });
        applyMode(inst);
        return true;
    }

    // Entra en «Calibrar con un rectángulo»: la vista previa se pinta en vivo y C# recibe el tamaño calculado (OnCalibrateChanged, 0 si aún no hay rectángulo).
    function beginGridCalibrate(minCell, maxCell) {
        const inst = core.current();
        if (!inst || inst.adjust || !inst.dto.grid) return false;
        const { cellSize, offsetX, offsetY } = inst.dto.grid;
        let lastSize = 0;
        const adjust = window.gridCalibrateMode.create({
            grid: { cellSize, offsetX, offsetY },
            toMap: point => worldPoint(inst, containerPoint(inst, { clientX: point.x, clientY: point.y })),
            onChange: () => {
                drawGrid(inst);
                window.gridCalibrateDraw.draw(inst);
                const size = adjust.cellSize() ?? 0;
                if (size === lastSize) return; // el rectángulo en vivo no cambia el tamaño en cada movimiento
                lastSize = size;
                core.notify(inst, 'OnCalibrateChanged', size);
            },
        });
        inst.adjust = adjust;
        applyMode(inst);
        return true;
    }

    // Columnas y filas que abarca el rectángulo; no hace nada fuera del modo de calibrar.
    function setCalibrateCells(columns, rows) {
        const inst = core.current();
        if (inst && inst.adjust && inst.adjust.setCells) inst.adjust.setCells(columns, rows);
    }

    // Sale del modo y devuelve la cuadrícula ajustada [cellSize, offsetX, offsetY]; con `commit` C# la guardará, si no se descarta.
    function endGridAdjust(commit) {
        const inst = core.current();
        if (!inst || !inst.adjust) return null;
        const adjusted = inst.adjust.grid();
        window.gridCalibrateDraw.clear(inst);
        const { cellSize, offsetX, offsetY } = adjusted;
        inst.adjust = null;
        // Al aceptar se conserva lo ajustado hasta que llegue el repintado de C#: la cuadrícula no parpadea.
        if (commit) inst.dto = { ...inst.dto, grid: { ...inst.dto.grid, ...adjusted } };
        applyMode(inst);
        drawGrid(inst); // vuelve a la cuadrícula de C# (o a la nueva, cuando llegue el repintado)
        return [cellSize, offsetX, offsetY];
    }

    function fit() {
        if (!core.current()) return;
        core.current().needsFit = true;
        tryFit(core.current());
    }

    // Centro de la vista en coordenadas del mapa: .NET coloca ahí los tokens nuevos.
    function viewCenter() {
        if (!core.current()) return null;
        const stage = core.current().stage;
        const scale = stage.scaleX();
        return [(stage.width() / 2 - stage.x()) / scale, (stage.height() / 2 - stage.y()) / scale];
    }

    // Zona del mapa que se ve [x, y, ancho, alto]: .NET la guarda como encuadre de la TV.
    function viewRect() {
        return core.current() ? window.tvFrame.viewRect(core.current().stage) : null;
    }

    // Rectángulo del encuadre de la TV, o null para quitarlo. Vive en su capa: los repintados no lo tocan.
    function setTvFrame(frame) {
        const inst = core.current();
        if (!inst) return;
        const layer = inst.layers.tvFrame;
        layer.destroyChildren();
        const config = window.tvFrame.tvFrameRect(frame, theme(inst.host).accent);
        if (config) layer.add(new Konva.Rect(config));
        layer.batchDraw();
    }

    // Área del mapa en el viewport (left, top, width, height): .NET convierte con ella el toque de un token a px del área.
    function hostRect() {
        if (!core.current()) return null;
        const rect = core.current().host.getBoundingClientRect();
        return [rect.left, rect.top, rect.width, rect.height];
    }

    window.mapCanvas = { load, render, setMode, setFogTool, fogDraft, fit, viewCenter, viewRect, hostRect, setTvFrame, beginGridAdjust, beginGridCalibrate, setCalibrateCells, endGridAdjust, dispose };
})();
