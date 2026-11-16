// Vista de jugadores de la tablet: pinta dentro de la app, con el dibujante de la TV (tv-map.js y tv-strip.js), el mapa
// abierto tal como lo ven los jugadores. Solo mira: el contenedor no escucha toques propios.
(function (root) {
    'use strict';

    var current = null;

    function parse(map) {
        return typeof map === 'string' ? JSON.parse(map) : map;
    }

    function show(host, map) {
        hide();
        var strip = document.createElement('div');
        strip.className = 'tv-strip';
        strip.hidden = true;
        var stage = document.createElement('div');
        stage.className = 'player-preview__canvas';
        host.appendChild(stage);
        host.appendChild(strip);

        // Mide el contenedor (no la ventana) y descuenta la tira, que ocupa la parte de arriba.
        function viewport() {
            var rect = host.getBoundingClientRect();
            return root.tvLayout.availableViewport({ width: rect.width, height: rect.height }, root.tvStrip.height(strip));
        }

        var instance = root.tvMap.create({ container: stage, viewport: viewport });
        var observer = null;
        if (typeof ResizeObserver !== 'undefined') {
            observer = new ResizeObserver(function () { instance.resize(); });
            observer.observe(host);
        }
        root.addEventListener('resize', instance.resize);
        current = { host: host, strip: strip, stage: stage, instance: instance, observer: observer };
        update(map);
    }

    function update(map) {
        if (!current) return;
        var data = parse(map);
        // Primero la tira: el mapa se encaja en el alto que ella deja libre.
        root.tvStrip.render(current.strip, data.combat || null);
        current.instance.render(data);
    }

    function hide() {
        if (!current) return;
        root.removeEventListener('resize', current.instance.resize);
        if (current.observer) current.observer.disconnect();
        current.instance.dispose();
        if (current.strip.parentNode) current.strip.parentNode.removeChild(current.strip);
        if (current.stage.parentNode) current.stage.parentNode.removeChild(current.stage);
        current = null;
    }

    root.playerPreview = { show: show, update: update, hide: hide };
})(window);
