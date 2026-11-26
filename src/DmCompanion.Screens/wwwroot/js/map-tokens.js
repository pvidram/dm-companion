// Tokens del lienzo del mapa (map-canvas.js): pintado, marcas de combate y avisos de movimiento o toque.
(function (root) {
    'use strict';

    const { ignore, notify, theme } = root.mapCore;

    function portrait(inst, url) {
        if (!url) return null;
        let entry = inst.portraits.get(url);
        if (!entry) {
            const image = new Image();
            entry = { image, loaded: false };
            inst.portraits.set(url, entry);
            image.onload = () => {
                entry.loaded = true;
                if (root.mapCore.current() === inst) drawTokens(inst);
            };
            image.onerror = ignore; // sin retrato se queda la inicial
            image.src = url;
        }
        return entry.loaded ? entry.image : null;
    }

    // Marcas del combate: turno actual (anillo exterior de acento), selección (contorno discontinuo),
    // 0 HP (token atenuado), muerto (más atenuado y con cruz) y estados (insignia con su número; de aviso si alguno está vencido).
    // Insignia de ojo tachado abajo a la derecha (como `TokenAvatar`): círculo violeta, ojo y barra, sin fuentes de iconos.
    function addHiddenBadge(group, radius, colors) {
        const badgeRadius = Math.max(10, radius * 0.275);
        const offset = radius * Math.SQRT1_2;
        const badge = new Konva.Group({ x: offset, y: offset, listening: false });
        const line = { stroke: colors.hiddenBadgeIcon, strokeWidth: 2, strokeScaleEnabled: false, lineCap: 'round', lineJoin: 'round' };
        badge.add(new Konva.Circle({ radius: badgeRadius, fill: colors.hiddenBadge, stroke: colors.outlineLight, strokeWidth: 1, strokeScaleEnabled: false }));
        const w = badgeRadius * 0.6;
        const h = badgeRadius * 0.34;
        badge.add(new Konva.Line({ points: [-w, 0, 0, -h, w, 0, 0, h], closed: true, tension: 0.45, ...line }));
        badge.add(new Konva.Circle({ radius: badgeRadius * 0.12, fill: colors.hiddenBadgeIcon, strokeEnabled: false }));
        badge.add(new Konva.Line({ points: [-w, -w, w, w], ...line }));
        group.add(badge);
    }

    function addCombatMarks(group, token, radius, colors) {
        const overlay = { strokeScaleEnabled: false, listening: false };
        if (token.current) {
            group.add(new Konva.Circle({ radius: radius + 9, stroke: colors.outlineDark, strokeWidth: 8, ...overlay }));
            group.add(new Konva.Circle({ radius: radius + 9, stroke: colors.accent, strokeWidth: 4, ...overlay }));
        }
        if (token.selected) {
            group.add(new Konva.Circle({ radius: radius + 5, stroke: colors.outlineLight, strokeWidth: 3, dash: [8, 5], ...overlay }));
        }
        if (token.conditions > 0) {
            const badgeRadius = 12;
            const offset = radius * Math.SQRT1_2;
            const badge = new Konva.Group({ x: offset, y: -offset, listening: false });
            badge.add(new Konva.Circle({
                radius: badgeRadius, fill: token.expired ? colors.danger : colors.sheet,
                stroke: colors.outlineDark, strokeWidth: 2, strokeScaleEnabled: false,
            }));
            badge.add(new Konva.Text({
                text: String(token.conditions), fontSize: 14, fontFamily: colors.font, fontStyle: 'bold', fill: token.expired ? colors.textOnDanger : colors.text,
                width: badgeRadius * 2, height: badgeRadius * 2, offsetX: badgeRadius, offsetY: badgeRadius,
                align: 'center', verticalAlign: 'middle',
            }));
            group.add(badge);
        }
        if (token.hidden) addHiddenBadge(group, radius, colors);
        if (token.dead) {
            // Muerto: más atenuado que a 0 HP y una cruz (dos líneas) sobre el avatar, sin fuentes de iconos.
            const arm = radius * 0.55;
            for (const [x1, y1, x2, y2] of [[-arm, -arm, arm, arm], [-arm, arm, arm, -arm]]) {
                group.add(new Konva.Line({ points: [x1, y1, x2, y2], stroke: colors.outlineDark, strokeWidth: 9, lineCap: 'round', ...overlay }));
                group.add(new Konva.Line({ points: [x1, y1, x2, y2], stroke: colors.outlineLight, strokeWidth: 5, lineCap: 'round', ...overlay }));
            }
        }
    }

    function tokenNode(inst, token, colors) {
        const cell = (inst.dto.grid && inst.dto.grid.cellSize) || 70;
        const radius = (token.size * cell) / 2;
        const ring = colors[token.faction] || colors.pc;
        const group = new Konva.Group({ x: token.x, y: token.y, draggable: !inst.dto.readOnly, id: token.id });

        // Anillo con doble contorno (claro y oscuro) para que se lea sobre cualquier imagen:
        // claro | oscuro | color del bando | oscuro | claro.
        // El cuerpo (avatar y anillos) se atenúa solo; las marcas de combate (cruz, turno, insignia) quedan a opacidad normal.
        const body = new Konva.Group({ opacity: window.tokenOpacity.tokenBodyOpacity(token) });
        group.add(body);
        const face = new Konva.Circle({ radius, fill: colors.sheetSunken, stroke: colors.outlineLight, strokeWidth: 7, strokeScaleEnabled: false });
        body.add(face);
        body.add(new Konva.Circle({ radius, stroke: colors.outlineDark, strokeWidth: 5, strokeScaleEnabled: false }));

        const inner = Math.max(1, radius - 3);
        const image = portrait(inst, token.portraitUrl);
        if (image) {
            const side = Math.min(image.naturalWidth, image.naturalHeight) || 1;
            const scale = (2 * inner) / side;
            body.add(new Konva.Circle({
                radius: inner,
                fillPatternImage: image,
                fillPatternRepeat: 'no-repeat',
                fillPatternScale: { x: scale, y: scale },
                fillPatternOffset: { x: image.naturalWidth / 2, y: image.naturalHeight / 2 },
            }));
        } else {
            const fontSize = Math.max(8, inner * 1.1);
            body.add(new Konva.Text({
                text: token.initial || '?', fontSize, fontFamily: colors.font, fontStyle: 'bold', fill: colors.text,
                width: inner * 2, height: inner * 2, offsetX: inner, offsetY: inner, align: 'center', verticalAlign: 'middle',
                listening: false,
            }));
        }
        body.add(new Konva.Circle({ radius, stroke: ring, strokeWidth: 3, strokeScaleEnabled: false, listening: false }));
        addCombatMarks(group, token, radius, colors);

        group.on('dragstart', () => group.moveToTop());
        group.setAttr('hitRadius', radius);
        group.on('dragend', () => commitTokenMove(inst, group, token));
        // Konva no emite click/tap si hubo arrastre, así que arrastrar un token nunca abre su tarjeta.
        group.on('click tap', () => notifyTokenTapped(inst, group, token.id));
        return group;
    }

    // Fin de un movimiento de token (arrastre en Navegar o lápiz en Dibujar).
    // Un pellizco que corta el arrastre no es un movimiento: se devuelve el token a su sitio.
    function commitTokenMove(inst, group, token) {
        if (inst.pinching) {
            group.position({ x: token.x, y: token.y });
            inst.layers.tokens.batchDraw();
            return;
        }
        notify(inst, 'OnTokenMoved', token.id, group.x(), group.y());
    }

    function notifyTokenTapped(inst, group, tokenId) {
        const rect = inst.host.getBoundingClientRect();
        const center = group.getAbsolutePosition();
        notify(inst, 'OnTokenTapped', tokenId, rect.left + center.x, rect.top + center.y);
    }

    function drawTokens(inst) {
        const layer = inst.layers.tokens;
        layer.destroyChildren();
        const colors = theme(inst.host);
        for (const token of inst.dto.tokens || []) layer.add(tokenNode(inst, token, colors));
        layer.batchDraw();
    }

    root.mapTokens = { drawTokens, notifyTokenTapped };
})(window);
