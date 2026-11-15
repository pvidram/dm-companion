// Página de la TV: solo mira. Recibe el estado completo por Server-Sent Events y lo pinta.
// ES2017 sin módulos (navegadores de Smart TV antiguos): nada de «?.», «??» ni campos privados.
(function () {
    'use strict';

    var RETRY_MS = 3000;
    var RESIZE_DEBOUNCE_MS = 250;

    var source = null;
    var resizeTimer = null;
    var reconnecting = document.getElementById('tv-reconnecting');

    // Un manejador por «kind». Lo desconocido se ignora.
    var handlers = {
        map: function (message) {
            if (!message.map) return;
            show('map');
            // Primero la tira: el mapa se encaja en el alto que ella deja libre.
            window.tvStrip.render(document.getElementById('tv-strip'), message.map.combat || null);
            window.tvMap.render(message.map);
        },
        content: function (message) {
            document.getElementById('tv-content-title').textContent = message.title || '';
            renderSheet(window.tvLayout.sheetView(message.sheet));
            // Único sitio donde se inserta HTML: ya viene filtrado y saneado por el servidor (NoteHtmlRenderer, Players).
            document.getElementById('tv-content-body').innerHTML = message.html || '';
            show('content');
            fitContent();
        },
        waiting: function (message) {
            document.getElementById('tv-campaign').textContent = message.campaign || '';
            show('waiting');
        }
    };

    // Ficha de PJ: retrato y estadísticas siempre como texto (textContent), nunca como HTML.
    function renderSheet(sheet) {
        var container = document.getElementById('tv-content-sheet');
        var portrait = document.getElementById('tv-sheet-portrait');
        var stats = document.getElementById('tv-sheet-stats');
        while (stats.firstChild) stats.removeChild(stats.firstChild);
        container.hidden = !sheet;
        if (!sheet) {
            portrait.hidden = true;
            portrait.removeAttribute('src');
            return;
        }
        portrait.hidden = !sheet.portraitUrl;
        if (sheet.portraitUrl) portrait.setAttribute('src', sheet.portraitUrl);
        else portrait.removeAttribute('src');
        for (var i = 0; i < sheet.stats.length; i++) {
            var item = document.createElement('div');
            item.className = 'tv-sheet__stat';
            var label = document.createElement('dt');
            label.textContent = sheet.stats[i].label;
            var value = document.createElement('dd');
            value.textContent = sheet.stats[i].value;
            item.appendChild(label);
            item.appendChild(value);
            stats.appendChild(item);
        }
    }

    // Letra grande, pero que quepa: baja la escala hasta 0.6 y, si aun así desborda, difumina el borde inferior.
    function fitContent() {
        var content = document.querySelector('.tv-content');
        var title = document.getElementById('tv-content-title');
        var picked = window.tvLayout.pickContentScale(function (scale) {
            content.style.fontSize = (2.6 * scale) + 'vw';
            title.style.fontSize = (5.5 * scale) + 'vw';
            return content.scrollHeight <= content.clientHeight;
        });
        content.className = picked.overflow ? 'tv-content tv-content--fade' : 'tv-content';
    }

    function show(view) {
        var views = document.querySelectorAll('[data-view]');
        for (var i = 0; i < views.length; i++) {
            views[i].hidden = views[i].getAttribute('data-view') !== view;
        }
    }

    function dispatch(data) {
        var message;
        try {
            message = JSON.parse(data);
        } catch (e) {
            return;
        }
        if (!message || typeof message.kind !== 'string') return;
        var handler = handlers.hasOwnProperty(message.kind) ? handlers[message.kind] : null;
        if (handler) handler(message);
    }

    function reportSize() {
        var request = new XMLHttpRequest();
        request.open('GET', 'size?w=' + Math.round(window.innerWidth) + '&h=' + Math.round(window.innerHeight), true);
        request.send();
    }

    function setReconnecting(on) {
        reconnecting.hidden = !on;
    }

    function connect() {
        source = new EventSource('events');
        source.onopen = function () {
            setReconnecting(false);
            reportSize(); // el servidor puede haberse reiniciado: vuelve a saber el tamaño
        };
        source.onmessage = function (event) {
            dispatch(event.data);
        };
        source.onerror = function () {
            setReconnecting(true);
            // EventSource reintenta solo salvo que el servidor responda con error (p. ej. 404): entonces se reintenta a mano.
            if (source.readyState === EventSource.CLOSED) {
                source.close();
                setTimeout(connect, RETRY_MS);
            }
        };
    }

    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(reportSize, RESIZE_DEBOUNCE_MS);
        window.tvMap.resize();
        if (!document.querySelector('[data-view="content"]').hidden) fitContent();
    });
    // Las imágenes del contenido (y el retrato de la ficha) cambian el alto al cargar (el evento load no burbujea: se captura).
    document.querySelector('.tv-content').addEventListener('load', function () {
        if (!document.querySelector('[data-view="content"]').hidden) fitContent();
    }, true);

    window.dmTv = {
        register: function (kind, handler) { handlers[kind] = handler; },
        show: show
    };

    reportSize();
    connect();
})();
