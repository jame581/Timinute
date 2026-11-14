// Idle detector for the running timer. Records the last user activity (throttled to one update
// per 5 s); when activity resumes after at least `idleMinutes`, reports the idle period to .NET.
// Each tab watches its own activity; a hidden tab simply sees no input.

const EVENTS = ['pointermove', 'pointerdown', 'keydown', 'wheel', 'touchstart'];
const THROTTLE_MS = 5000;

let ref = null;
let thresholdMs = 0;
let lastActivity = 0;
let lastUpdate = 0;
let watching = false;

function onActivity() {
    const now = Date.now();
    if (thresholdMs > 0 && lastActivity > 0 && now - lastActivity >= thresholdMs && ref) {
        const start = new Date(lastActivity).toISOString();
        const end = new Date(now).toISOString();
        ref.invokeMethodAsync('OnIdleEnded', start, end).catch(() => { });
    }
    if (now - lastUpdate >= THROTTLE_MS || now - lastActivity >= thresholdMs) {
        lastActivity = now;
        lastUpdate = now;
    }
}

function onVisibility() {
    // Coming back to the tab counts as activity; leaving it does not reset the clock.
    if (document.visibilityState === 'visible') onActivity();
}

export function register(dotnetRef) {
    ref = dotnetRef;
}

export function startIdleWatch(idleMinutes) {
    thresholdMs = Math.max(0, idleMinutes) * 60 * 1000;
    if (watching) return;
    watching = true;
    lastActivity = Date.now();
    lastUpdate = lastActivity;
    EVENTS.forEach(e => window.addEventListener(e, onActivity, { passive: true, capture: true }));
    document.addEventListener('visibilitychange', onVisibility);
}

export function stopIdleWatch() {
    if (!watching) return;
    watching = false;
    EVENTS.forEach(e => window.removeEventListener(e, onActivity, { capture: true }));
    document.removeEventListener('visibilitychange', onVisibility);
}
