// Enlaces externos en la ventana de escritorio: se abren en el navegador del sistema, nunca dentro de la app.
// isExternalLink es pura y se prueba con `node --test` (tests/js/desktop-links.test.js).
(function (root) {
    'use strict';

    function isExternalLink(href, appOrigin) {
        if (!href) return false;
        var url;
        try {
            url = new URL(href, appOrigin);
        } catch (e) {
            return false;
        }
        return (url.protocol === 'http:' || url.protocol === 'https:') && url.origin !== new URL(appOrigin).origin;
    }

    function openExternal(href) {
        return fetch('/_desktop/open-external', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ url: new URL(href, root.location.origin).href })
        }).catch(function () {
            console.warn('No se pudo abrir el enlace externo.');
        });
    }

    function install() {
        // El clic central (auxclick) abriría una ventana emergente de WebView2: se trata igual que el clic normal.
        function handle(event) {
            var anchor = event.target instanceof Element ? event.target.closest('a[href]') : null;
            if (!anchor || !isExternalLink(anchor.getAttribute('href'), root.location.origin)) return;
            event.preventDefault();
            event.stopPropagation();
            openExternal(anchor.getAttribute('href'));
        }
        root.document.addEventListener('click', handle, true);
        root.document.addEventListener('auxclick', function (event) {
            if (event.button === 1) handle(event);
        }, true);

        var originalOpen = root.open;
        root.open = function (href) {
            if (isExternalLink(href, root.location.origin)) {
                openExternal(href);
                return null;
            }
            return originalOpen.apply(root, arguments);
        };
    }

    var api = { isExternalLink: isExternalLink };
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (typeof window !== 'undefined' && root.document) install();
    root.desktopLinks = api;
})(typeof window !== 'undefined' ? window : globalThis);
