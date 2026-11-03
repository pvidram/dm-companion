window.dmWikilinks = {
    attach(element, dotnetRef) {
        if (!element || element.__dmWikilinks) return;
        element.__dmWikilinks = true;
        element.addEventListener('click', event => {
            const anchor = event.target instanceof Element ? event.target.closest('a.wikilink') : null;
            if (!anchor || !element.contains(anchor)) return;
            event.preventDefault();
            const target = anchor.dataset.target;
            if (!target) return;
            dotnetRef.invokeMethodAsync('OnWikilinkClicked', target, anchor.classList.contains('broken')).catch(() => {});
        });
    }
};
