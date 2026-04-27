// Viewport breakpoint bridge — pushes matchMedia state to .NET.
// Called once from ViewportService on first render.

let mediaQuery = null;
let dotnetRef = null;

function notify() {
    if (dotnetRef) {
        try {
            dotnetRef.invokeMethodAsync('SetIsMobile', mediaQuery.matches);
        } catch {
            // Connection torn down (page unload, hot reload). Safe to ignore.
        }
    }
}

export function register(reference) {
    dotnetRef = reference;
    if (!mediaQuery) {
        mediaQuery = window.matchMedia('(max-width: 768px)');
        mediaQuery.addEventListener('change', notify);
    }
    // Push initial value immediately so the cascade is correct on first render.
    notify();
}

export function unregister() {
    if (mediaQuery) {
        mediaQuery.removeEventListener('change', notify);
        mediaQuery = null;
    }
    dotnetRef = null;
}
