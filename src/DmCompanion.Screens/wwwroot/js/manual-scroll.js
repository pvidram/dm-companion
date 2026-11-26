// Cálculos puros de la lectura continua del visor de manuales (alturas de huecos, ventana de pintado, página actual,
// compensación de desplazamiento y anclaje de zoom): se prueban con `node --test` (tests/js).
(function (root) {
    'use strict';

    // Posición de cada hueco en la columna (con `gap` entre ellos) y altura total.
    function layout(heights, gap) {
        var tops = [];
        var y = 0;
        for (var i = 0; i < heights.length; i++) {
            tops.push(y);
            y += heights[i] + gap;
        }
        return { tops: tops, total: Math.max(0, y - gap) };
    }

    // Altura del hueco de una página de pageW × pageH cuando se pinta a cssWidth de ancho.
    function slotHeight(pageW, pageH, cssWidth) {
        if (!(pageW > 0) || !(pageH > 0)) return Math.round(cssWidth);
        return Math.max(1, Math.round(cssWidth * pageH / pageW));
    }

    // Página cuyo hueco ocupa más altura visible; con empate, la de número más bajo.
    function currentPage(tops, heights, scrollTop, viewH) {
        var best = 1;
        var bestOverlap = -Infinity;
        for (var i = 0; i < tops.length; i++) {
            var overlap = Math.min(tops[i] + heights[i], scrollTop + viewH) - Math.max(tops[i], scrollTop);
            if (overlap > bestOverlap) {
                best = i + 1;
                bestOverlap = overlap;
            }
        }
        return best;
    }

    // Páginas que hay que tener pintadas: las que tocan la vista con `margin` por arriba y por abajo, y la actual con una vecina a cada lado.
    function paintWindow(tops, heights, scrollTop, viewH, margin, current) {
        var count = tops.length;
        if (count === 0) return { first: 1, last: 0 };
        var first = Math.max(1, current - 1);
        var last = Math.min(count, current + 1);
        var from = scrollTop - margin;
        var to = scrollTop + viewH + margin;
        for (var i = 0; i < count; i++) {
            if (tops[i] + heights[i] > from && tops[i] < to) {
                first = Math.min(first, i + 1);
                last = Math.max(last, i + 1);
            }
        }
        return { first: first, last: last };
    }

    // Nuevo scrollTop cuando un hueco cambia de altura: si está entero por encima de la vista, se desplaza la diferencia;
    // si la vista empieza dentro de él, se conserva la proporción; si está por debajo, no cambia nada.
    function compensateScroll(scrollTop, slotTop, oldHeight, newHeight) {
        if (slotTop + oldHeight <= scrollTop) return scrollTop + (newHeight - oldHeight);
        if (slotTop < scrollTop && oldHeight > 0) return slotTop + (scrollTop - slotTop) * newHeight / oldHeight;
        return scrollTop;
    }

    // Página y fracción de ella (0 = su borde superior) que están en lo alto de la vista; la fracción puede ser negativa.
    function anchorAt(tops, heights, scrollTop, page) {
        var height = heights[page - 1];
        return { page: page, fraction: height > 0 ? (scrollTop - tops[page - 1]) / height : 0 };
    }

    // scrollTop que deja el ancla en lo alto de la vista con las alturas actuales.
    function scrollForAnchor(tops, heights, anchor) {
        return tops[anchor.page - 1] + anchor.fraction * heights[anchor.page - 1];
    }

    // Píxeles que le tocan a cada canvas cuando `pages` páginas comparten el tope `total` de la ventana.
    function pixelBudget(total, pages) {
        return Math.floor(total / Math.max(1, pages));
    }

    // transform-origin de la vista previa del zoom: lo alto y la izquierda de la vista, en coordenadas de la columna.
    function previewOrigin(scrollLeft, scrollTop, columnLeft, columnTop) {
        return (scrollLeft - columnLeft) + 'px ' + (scrollTop - columnTop) + 'px';
    }

    // Fracción del ancho de la columna que queda a la izquierda de la vista (negativa si la columna empieza dentro de ella).
    function horizontalFraction(scrollLeft, columnLeft, columnWidth) {
        return columnWidth > 0 ? (scrollLeft - columnLeft) / columnWidth : 0;
    }

    // scrollLeft que deja esa fracción a la izquierda de la vista con el ancho y la posición actuales de la columna.
    function scrollForHorizontal(columnLeft, columnWidth, fraction) {
        return Math.max(0, columnLeft + fraction * columnWidth);
    }

    // Tamaños de las páginas: las que aún no se han leído toman el último tamaño leído, no el de la página 1.
    function fillUnknownSizes(sizes, known, lastRead) {
        return sizes.map(function (size, i) { return known[i] ? size : lastRead; });
    }

    // Nuevo scrollTop tras cambiar varios huecos a la vez: sigue al hueco que contiene el scrollTop antiguo (en proporción
    // dentro de él, o con el mismo desfase si cae en el espacio entre huecos).
    function remapScroll(oldTops, oldHeights, newTops, newHeights, scrollTop) {
        var i = 0;
        for (var n = 0; n < oldTops.length; n++) {
            if (oldTops[n] <= scrollTop) i = n;
        }
        if (oldTops.length === 0) return scrollTop;
        var offset = scrollTop - oldTops[i];
        if (offset >= oldHeights[i]) return newTops[i] + newHeights[i] + (offset - oldHeights[i]);
        return newTops[i] + (oldHeights[i] > 0 ? offset * newHeights[i] / oldHeights[i] : 0);
    }

    var api = {
        layout: layout, slotHeight: slotHeight, currentPage: currentPage, paintWindow: paintWindow,
        compensateScroll: compensateScroll, anchorAt: anchorAt, scrollForAnchor: scrollForAnchor, pixelBudget: pixelBudget,
        previewOrigin: previewOrigin, horizontalFraction: horizontalFraction, scrollForHorizontal: scrollForHorizontal,
        fillUnknownSizes: fillUnknownSizes, remapScroll: remapScroll
    };
    root.manualScroll = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
