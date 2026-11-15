// Opt-in OS notifications for timer warnings. The tag collapses the same warning raised by
// several open tabs into one notification; clicking it focuses the tab and opens the bell.

let openBellRef = null;

export function permission() {
    return 'Notification' in window ? Notification.permission : 'unsupported';
}

export async function request() {
    if (!('Notification' in window)) return 'unsupported';
    try {
        return await Notification.requestPermission();
    } catch {
        return Notification.permission;
    }
}

export function show(title, body, tag) {
    if (permission() !== 'granted') return;
    try {
        const n = new Notification(title, { body, tag, renotify: false });
        n.onclick = () => {
            window.focus();
            n.close();
            if (openBellRef) openBellRef.invokeMethodAsync('OnOpenBellRequested').catch(() => { });
        };
    } catch {
        // Some mobile browsers only allow notifications from a service worker; the bell still shows it.
    }
}

export function registerOpenBell(ref) {
    openBellRef = ref;
}
