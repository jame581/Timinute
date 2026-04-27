// Cmd/Ctrl+K focus shortcut for the Aurora topbar search input.
// Loaded lazily by Topbar.razor as an ES module via JS interop.

let handler = null;

export function registerCmdKFocus(selector) {
    if (handler) return;
    handler = function (e) {
        if ((e.metaKey || e.ctrlKey) && e.key && e.key.toLowerCase() === 'k') {
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
