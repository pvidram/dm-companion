// Pila de ámbitos de foco: el último abierto es el activo. Solo los diálogos atrapan Tab.
const scopes = [];
const FOCUSABLE = 'button:not([disabled]), a[href], area[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

function isTabbable(el) {
    if (!el.matches(FOCUSABLE) || el.tabIndex < 0 || el.matches('input[type="hidden"]')) return false;
    if (el.closest('[hidden], [inert]')) return false;
    return el.checkVisibility
        ? el.checkVisibility({ visibilityProperty: true })
        : el.getClientRects().length > 0;
}

function focusables(container) {
    return [...container.querySelectorAll(FOCUSABLE)].filter(isTabbable);
}

// Descarta ámbitos cuyo contenedor ya no está en el DOM (diálogos desmontados sin llamar a exit).
function prune() {
    for (let i = scopes.length - 1; i >= 0; i--) {
        const { container } = scopes[i];
        if (!container || !container.isConnected) {
            cancelAnimationFrame(scopes[i].frame);
            scopes.splice(i, 1);
        }
    }
}

function onKeydown(event) {
    if (event.key !== 'Tab') return;
    prune();
    const scope = scopes[scopes.length - 1];
    if (!scope || !scope.trap) return;
    const items = focusables(scope.container);
    if (items.length === 0) { event.preventDefault(); return; }
    const first = items[0];
    const last = items[items.length - 1];
    if (!scope.container.contains(document.activeElement) || document.activeElement === scope.container) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
}

document.addEventListener('keydown', onKeydown, true);

export function enter(container, trap) {
    prune();
    const scope = { container, trap, previous: document.activeElement, frame: 0 };
    scopes.push(scope);
    scope.frame = requestAnimationFrame(() => (focusables(container)[0] ?? container).focus());
}

export function exit(container) {
    prune();
    if (!container) return;
    const index = scopes.findLastIndex(scope => scope.container === container);
    if (index < 0) return;
    const wasTop = index === scopes.length - 1;
    const [scope] = scopes.splice(index, 1);
    cancelAnimationFrame(scope.frame);
    if (wasTop && scope.previous && document.contains(scope.previous)) scope.previous.focus();
}
