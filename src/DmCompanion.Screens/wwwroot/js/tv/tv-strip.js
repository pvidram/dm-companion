// Tira de iniciativa de la TV (arriba). Todo con textContent: ningún texto del mapa se interpreta como HTML.
(function (root) {
    'use strict';

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function entryNode(entry) {
        var classes = 'tv-strip__entry';
        if (entry.current) classes += ' tv-strip__entry--current';
        if (entry.dead) classes += ' tv-strip__entry--dead';
        var node = el('li', classes);
        node.appendChild(el('span', 'tv-strip__initiative', entry.initiative === undefined || entry.initiative === null ? '' : String(entry.initiative)));
        node.appendChild(el('span', 'tv-strip__name', entry.name || ''));
        var hp = root.tvLayout.hpClass(entry.hp);
        if (hp) node.appendChild(el('span', 'tv-strip__hp tv-strip__hp--' + hp, entry.hp));
        var conditions = entry.conditions || [];
        if (conditions.length > 0) node.appendChild(el('span', 'tv-strip__conditions', conditions.join(' · ')));
        return node;
    }

    // Redibuja la tira entera. Sin combate (o sin entradas) se oculta.
    function render(container, combat) {
        while (container.firstChild) container.removeChild(container.firstChild);
        var entries = combat ? root.tvLayout.visibleStripEntries(combat.entries) : [];
        container.hidden = !combat || entries.length === 0;
        if (container.hidden) return;
        container.appendChild(el('span', 'tv-strip__round', 'Ronda ' + combat.round));
        var list = el('ol', 'tv-strip__list');
        for (var i = 0; i < entries.length; i++) list.appendChild(entryNode(entries[i]));
        container.appendChild(list);
        // La lista tiene alto máximo (varias filas); si el turno cae fuera, se desplaza hasta mostrarlo.
        var current = list.querySelector('.tv-strip__entry--current');
        if (current && current.offsetTop + current.offsetHeight > list.clientHeight) list.scrollTop = current.offsetTop;
    }

    // Alto real de la tira (0 si está oculta), para que el mapa se ajuste al resto de la pantalla.
    function height(container) {
        return container.hidden ? 0 : container.offsetHeight;
    }

    root.tvStrip = { render: render, height: height };
})(window);
