// Núcleo compartido de los módulos del lienzo del mapa (map-canvas.js): instancia actual, avisos a C#, tema y coordenadas.
(function (root) {
    'use strict';

    const DEFAULT_WIDTH = 2800;
    const DEFAULT_HEIGHT = 2100;
    // Un único mapa a la vez.
    let instance = null;
    const current = () => instance;
    const setCurrent = value => { instance = value; };

    function ignore() { /* la llamada a C# puede fallar si el circuito se cerró; no hay nada que hacer */ }

    function notify(inst, method, ...args) {
        inst.dotnet.invokeMethodAsync(method, ...args).catch(ignore);
    }

    // Colores del design system (tokens semánticos), leídos en cada pintado para respetar el tema.
    function theme(host) {
        const root = host.closest('.ds-root') || document.documentElement;
        const css = getComputedStyle(root);
        const get = (name, fallback) => css.getPropertyValue(name).trim() || fallback;
        return {
            pc: get('--faction-pc', '#1f5fa8'),
            ally: get('--faction-ally', '#2f7d4a'),
            enemy: get('--faction-enemy', '#a42a2a'),
            outlineDark: get('--faction-outline-on-sheet', '#1b1712'),
            outlineLight: get('--faction-outline-on-wood', '#f2ead8'),
            accent: get('--accent-on-wood', '#C9A24A'),
            danger: get('--fill-damage', '#9E2B25'),
            textOnDanger: get('--text-on-wood', '#F2EAD8'),
            sheet: get('--surface-sheet', '#e4d5b0'),
            sheetSunken: get('--surface-sheet-sunken', '#d3c29a'),
            text: get('--text-ink', '#1b1712'),
            font: css.fontFamily || 'sans-serif',
            measureDark: get('--map-measure-line-dark', '#2B2118'),
            measureLight: get('--map-measure-line-light', '#E8CF8E'),
            measureDarkWidth: parseFloat(get('--map-measure-width-dark', '6px')),
            measureLightWidth: parseFloat(get('--map-measure-width-light', '3px')),
            labelBg: get('--map-measure-label-bg', '#2B2118'),
            labelText: get('--map-measure-label-text', '#F2EAD8'),
            hiddenBadge: get('--map-hidden-badge-bg', '#4F3570'),
            hiddenBadgeIcon: get('--map-hidden-badge-icon', '#E8DCC2'),
            labelFont: get('--font-body', '') || css.fontFamily || 'sans-serif',
        };
    }

    function mapSize(inst) {
        return { w: inst.mapW, h: inst.mapH };
    }

    // Posición del evento en el contenedor (px de pantalla) y en el mapa.
    function containerPoint(inst, event) {
        const rect = inst.host.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    function worldPoint(inst, screen) {
        const stage = inst.stage;
        const scale = stage.scaleX();
        return { x: (screen.x - stage.x()) / scale, y: (screen.y - stage.y()) / scale };
    }

    // Todos los puntos del evento, incluidos los que el navegador agrupó entre dos fotogramas.
    function eventPoints(event) {
        const coalesced = typeof event.getCoalescedEvents === 'function' ? event.getCoalescedEvents() : [];
        return coalesced.length > 0 ? coalesced : [event];
    }

    function pressureOf(event) {
        return event.pressure > 0 ? event.pressure : 0.5;
    }

    root.mapCore = { DEFAULT_WIDTH, DEFAULT_HEIGHT, ignore, notify, theme, mapSize, containerPoint, worldPoint, eventPoints, pressureOf, current, setCurrent };
})(window);
