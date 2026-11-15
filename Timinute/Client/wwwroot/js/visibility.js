// Tells TimerService to re-read the running timer when this tab becomes visible or focused,
// so a timer stopped on another device disappears here without waiting for the 60 s poll.
// `focus` and `visibilitychange` both fire when returning to a tab; collapse them into one call.
let handler = null;

export function registerVisibility(dotnetRef) {
    unregisterVisibility();
    let last = 0;
    handler = () => {
        if (document.visibilityState !== 'visible') return;
        const now = Date.now();
        if (now - last < 1000) return;
        last = now;
        dotnetRef.invokeMethodAsync('OnVisible').catch(() => { /* service disposed */ });
    };
    document.addEventListener('visibilitychange', handler);
    window.addEventListener('focus', handler);
}

export function unregisterVisibility() {
    if (!handler) return;
    document.removeEventListener('visibilitychange', handler);
    window.removeEventListener('focus', handler);
    handler = null;
}
