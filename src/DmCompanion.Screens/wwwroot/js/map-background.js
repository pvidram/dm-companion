// Imagen de fondo del lienzo del mapa (map-canvas.js): carga, reducción de imágenes enormes y tamaño del lienzo.
(function (root) {
    'use strict';

    const { DEFAULT_WIDTH, DEFAULT_HEIGHT, notify } = root.mapCore;
    const { drawAll } = root.mapLayers;
    const { tryFit } = root.mapView;

    function setCanvasSize(inst) {
        inst.mapW = inst.dto.width > 0 ? inst.dto.width : DEFAULT_WIDTH;
        inst.mapH = inst.dto.height > 0 ? inst.dto.height : DEFAULT_HEIGHT;
    }

    function backgroundKey(dto) {
        return dto.mapPath + '|' + (dto.imageUrl || ('canvas:' + dto.width + 'x' + dto.height));
    }

    function loadBackground(inst) {
        inst.bgImage = null;
        inst.needsFit = true;
        const ticket = ++inst.bgTicket;
        const dto = inst.dto;
        if (!dto.imageUrl) {
            setCanvasSize(inst);
            drawAll(inst);
            tryFit(inst);
            return;
        }

        // Hasta conocer el tamaño real de la imagen no hay nada que pintar ni encajar.
        inst.mapW = 0;
        inst.mapH = 0;
        drawAll(inst);
        const img = new Image();
        img.onload = async () => {
            if (inst.bgTicket !== ticket || root.mapCore.current() !== inst) return;
            const width = img.naturalWidth;
            const height = img.naturalHeight;
            let source = img;
            const side = Math.max(width, height);
            if (side > dto.maxImageSide) {
                // El WebView no pinta imágenes enormes: se reduce, pero el mapa conserva sus medidas originales.
                const k = dto.maxImageSide / side;
                try {
                    source = await createImageBitmap(img, {
                        resizeWidth: Math.max(1, Math.round(width * k)),
                        resizeHeight: Math.max(1, Math.round(height * k)),
                        resizeQuality: 'high',
                    });
                } catch {
                    source = img;
                }
                if (inst.bgTicket !== ticket || root.mapCore.current() !== inst) return;
            }
            inst.bgImage = source;
            inst.mapW = width;
            inst.mapH = height;
            drawAll(inst);
            tryFit(inst);
            notify(inst, 'OnImageLoaded', dto.mapPath, width, height);
        };
        img.onerror = () => {
            if (inst.bgTicket !== ticket || root.mapCore.current() !== inst) return;
            inst.mapW = DEFAULT_WIDTH;
            inst.mapH = DEFAULT_HEIGHT;
            drawAll(inst);
            tryFit(inst);
            notify(inst, 'OnImageFailed', dto.mapPath);
            notify(inst, 'OnImageLoaded', dto.mapPath, DEFAULT_WIDTH, DEFAULT_HEIGHT);
        };
        img.src = dto.imageUrl;
    }

    root.mapBackground = { setCanvasSize, backgroundKey, loadBackground };
})(window);
