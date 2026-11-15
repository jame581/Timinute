// Focus management for AuroraModal: keeps Tab inside the open dialog, focuses its first
// control on open and restores focus to the opener on close. While any dialog is open,
// <html> carries .aurora-modal-open (topbar-shortcuts.js checks it). Nested dialogs stack:
// only the topmost one traps.

const traps = [];

const FOCUSABLE = [
    'a[href]', 'button:not([disabled])', 'input:not([disabled]):not([type="hidden"])',
    'select:not([disabled])', 'textarea:not([disabled])', '[tabindex]:not([tabindex="-1"])',
].join(',');

function focusables(el) {
    return Array.from(el.querySelectorAll(FOCUSABLE))
        .filter(n => n.offsetParent !== null || n === document.activeElement);
}

function onKeyDown(e) {
    if (e.key !== 'Tab' || traps.length === 0) return;
    const { el } = traps[traps.length - 1];
    const items = focusables(el);
    if (items.length === 0) {
        e.preventDefault();
        el.focus();
        return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    if (e.shiftKey && (document.activeElement === first || !el.contains(document.activeElement))) {
        e.preventDefault();
        last.focus();
    } else if (!e.shiftKey && (document.activeElement === last || !el.contains(document.activeElement))) {
        e.preventDefault();
        first.focus();
    }
}

export function trap(el) {
    if (!el) return;
    traps.push({ el, returnTo: document.activeElement });
    if (traps.length === 1) {
        document.addEventListener('keydown', onKeyDown, true);
        document.documentElement.classList.add('aurora-modal-open');
    }

    // Prefer an explicit [data-autofocus], then the first field in the body, then the panel.
    const target = el.querySelector('[data-autofocus]')
        || el.querySelector('.aurora-modal__body input, .aurora-modal__body select, .aurora-modal__body textarea')
        || el;
    target.focus({ preventScroll: true });
}

export function release(el) {
    const index = traps.findIndex(t => t.el === el);
    if (index === -1) return;
    const [{ returnTo }] = traps.splice(index, 1);
    if (traps.length === 0) {
        document.removeEventListener('keydown', onKeyDown, true);
        document.documentElement.classList.remove('aurora-modal-open');
    }

    // With another dialog still open, focus must stay inside the (new) topmost one: use the
    // opener only if it lives there, otherwise the panel itself. Never focus behind a modal.
    const top = traps.length > 0 ? traps[traps.length - 1].el : null;
    const usable = returnTo && typeof returnTo.focus === 'function' && document.contains(returnTo);
    if (top) {
        if (top.contains(document.activeElement)) return;
        (usable && top.contains(returnTo) ? returnTo : top).focus({ preventScroll: true });
    } else if (usable) {
        returnTo.focus({ preventScroll: true });
    }
}
