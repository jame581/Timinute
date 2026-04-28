// Pre-Blazor theme bootstrap. Loaded synchronously in <head> before
// the Blazor framework script so the user's theme is applied on first
// paint — no flash of light-mode while WASM mounts.
//
// Source of truth is the server (UserPreferences.Theme), but localStorage
// is the cache that survives across reloads. Blazor syncs the cache
// from GetMe on app boot via window.__theme.set(...).

(function () {
    const KEY = 'timinute:theme';
    const root = document.documentElement;

    function resolve(stored) {
        if (stored === 'Dark') return 'dark';
        if (stored === 'Light') return 'light';
        // 'System' (or unset): follow OS preference.
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark'
            : 'light';
    }

    function apply(stored) {
        root.setAttribute('data-theme', resolve(stored));
    }

    let stored;
    try { stored = localStorage.getItem(KEY) || 'System'; } catch { stored = 'System'; }
    apply(stored);

    // OS-theme-change handler stays attached. The cur === 'System'
    // guard makes it a no-op once the user picks a fixed Light/Dark,
    // so we don't need to remove/re-add on toggle.
    if (window.matchMedia) {
        const mql = window.matchMedia('(prefers-color-scheme: dark)');
        if (mql.addEventListener) {
            mql.addEventListener('change', function () {
                let cur;
                try { cur = localStorage.getItem(KEY) || 'System'; } catch { cur = 'System'; }
                if (cur === 'System') apply('System');
            });
        }
    }

    // Exposed for Blazor's ThemeService to call after server sync or toggle.
    window.__theme = {
        set: function (stored) {
            try { localStorage.setItem(KEY, stored); } catch { /* private mode / quota — non-fatal */ }
            apply(stored);
        },
        get: function () {
            try { return localStorage.getItem(KEY) || 'System'; } catch { return 'System'; }
        },
    };
})();
