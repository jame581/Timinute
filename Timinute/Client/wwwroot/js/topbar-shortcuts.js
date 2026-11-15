// Cmd/Ctrl+K focus shortcut for the Aurora topbar search input.
// Loaded lazily by Topbar.razor as an ES module via JS interop.

let handler = null;

export function registerCmdKFocus(selector) {
    if (handler) return;
    handler = function (e) {
        if ((e.metaKey || e.ctrlKey) && e.key && e.key.toLowerCase() === 'k') {
            // Don't pull focus behind an open dialog (focus-trap.js sets this class).
            if (document.documentElement.classList.contains('aurora-modal-open')) return;
            const input = document.querySelector(selector);
            if (input) {
                e.preventDefault();
                input.focus();
                if (typeof input.select === 'function') {
                    input.select();
                }
            }
        }
    };
    document.addEventListener('keydown', handler);
}

export function unregisterCmdKFocus() {
    if (handler) {
        document.removeEventListener('keydown', handler);
        handler = null;
    }
}
