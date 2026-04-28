// GitHub stargazer count fetch with localStorage cache.
// Used by LandingHero to render the live "Star on GitHub · Nk" pill.
// Cache TTL is 6 hours — GitHub unauthenticated API limit is 60 req/hr per IP,
// and we don't want every landing-page visit to spend a request.

const CACHE_KEY_PREFIX = 'tim:gh-stars:';
const TTL_MS = 6 * 60 * 60 * 1000;

function format(count) {
    if (count == null || isNaN(count)) return null;
    if (count < 1000) return String(count);
    if (count < 10000) {
        // 1.2k (one decimal, trim trailing .0)
        const v = (count / 1000).toFixed(1);
        return (v.endsWith('.0') ? v.slice(0, -2) : v) + 'k';
    }
    return Math.round(count / 1000) + 'k';
}

export async function getStars(owner, repo) {
    const key = CACHE_KEY_PREFIX + owner + '/' + repo;

    try {
        const cached = window.localStorage.getItem(key);
        if (cached) {
            const parsed = JSON.parse(cached);
            if (parsed && typeof parsed.count === 'number' && typeof parsed.at === 'number') {
                if (Date.now() - parsed.at < TTL_MS) {
                    return format(parsed.count);
                }
            }
        }
    } catch {
        // localStorage disabled / corrupted entry — fall through to fetch.
    }

    try {
        const res = await fetch('https://api.github.com/repos/' + owner + '/' + repo, {
            headers: { 'Accept': 'application/vnd.github+json' },
        });
        if (!res.ok) return null;
        const data = await res.json();
        const count = data && typeof data.stargazers_count === 'number' ? data.stargazers_count : null;
        if (count == null) return null;

        try {
            window.localStorage.setItem(key, JSON.stringify({ count, at: Date.now() }));
        } catch {
            // Quota exceeded / private mode — caching is best-effort.
        }

        return format(count);
    } catch {
        return null;
    }
}
