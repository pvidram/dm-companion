// Página de la TV: solo mira. Recibe el estado completo por Server-Sent Events y lo pinta.
// ES2017 sin módulos (navegadores de Smart TV antiguos): nada de «?.», «??» ni campos privados.
(function () {
    'use strict';

    var RETRY_MS = 3000;
    var RESIZE_DEBOUNCE_MS = 250;

    var source = null;
    var resizeTimer = null;
    var reconnecting = document.getElementById('tv-reconnecting');

    // El mapa ocupa lo que deja libre la tira de iniciativa (mide su alto real).
    function viewport() {
        var strip = window.tvStrip.height(document.getElementById('tv-strip'));
        return window.tvLayout.availableViewport({ width: window.innerWidth, height: window.innerHeight }, strip);
    }

    var mapView = window.tvMap.create({ container: document.getElementById('tv-stage'), viewport: viewport });

    // Un manejador por «kind». Lo desconocido se ignora.
    var handlers = {
        map: function (message) {
            if (!message.map) return;
            show('map');
            // Primero la tira: el mapa se encaja en el alto que ella deja libre.
            window.tvStrip.render(document.getElementById('tv-strip'), message.map.combat || null);
            mapView.render(message.map);
        },
        content: function (message) {
            document.getElementById('tv-content-title').textContent = message.title || '';
            renderSheet(window.tvLayout.sheetView(message.sheet));
            // Único sitio donde se inserta HTML: ya viene filtrado y saneado por el servidor (NoteHtmlRenderer, Players).
            document.getElementById('tv-content-body').innerHTML = message.html || '';
            show('content');
            fitContent();
        },
        slide: function (message) {
            var view = window.tvLayout.slideView(message);
            var image = document.getElementById('tv-slide-image');
            var text = document.getElementById('tv-slide-text');
            image.hidden = !view.image;
            if (view.image) image.setAttribute('src', view.image);
            else image.removeAttribute('src');
            text.hidden = !view.text;
            text.textContent = view.text || ''; // texto plano: nunca HTML
            document.getElementById('tv-slide-caption').textContent = view.caption;
            show('slide');
            fitSlideText();
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

    // Misma regla que fitContent para la nota de una diapositiva: letra grande, baja hasta 0.6 y, si aun así no cabe, degradado.
    function fitSlideText() {
        var text = document.getElementById('tv-slide-text');
        if (text.hidden) return;
        var picked = window.tvLayout.pickContentScale(function (scale) {
            text.style.fontSize = (2.6 * scale) + 'vw';
            return text.scrollHeight <= text.clientHeight;
        });
        text.className = picked.overflow ? 'tv-slide__text tv-slide__text--fade' : 'tv-slide__text';
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
        mapView.resize();
        if (!document.querySelector('[data-view="content"]').hidden) fitContent();
        if (!document.querySelector('[data-view="slide"]').hidden) fitSlideText();
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
