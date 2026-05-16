using Microsoft.JSInterop;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos;

namespace Timinute.Client.Services
{
    // Single point of contact for theme state. Wraps the JS bootstrap
    // (window.__theme.set/get) and the PUT /User/me/preferences call.
    //
    // localStorage is a *cache* of the server's value, not the source of
    // truth. SyncFromServerAsync writes the server's preference back into
    // localStorage on app boot so cross-device toggles eventually catch up.
    public class ThemeService : IAsyncDisposable, IDisposable
    {
        private readonly IJSRuntime js;
        private readonly IHttpClientFactory clientFactory;
        private readonly UserProfileService profileService;
        private DotNetObjectReference<ThemeService>? selfRef;

        public ThemeService(IJSRuntime js, IHttpClientFactory clientFactory, UserProfileService profileService)
        {
            this.js = js;
            this.clientFactory = clientFactory;
            this.profileService = profileService;
        }

        public event Action<ThemePreference>? Changed;

        public async Task<ThemePreference> GetCurrentAsync()
        {
            try
            {
                var stored = await js.InvokeAsync<string?>("__theme.get");
                if (Enum.TryParse<ThemePreference>(stored, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }
            catch (JSException) { /* bootstrap script absent or torn down */ }
            catch (JSDisconnectedException) { /* circuit gone */ }

            return ThemePreference.System;
        }

        // Server sync: routes through UserProfileService so all callers share
        // a single cached GET /User/me per session. Silently no-ops if
        // unauthenticated or the call fails — the bootstrap script already
        // applied a default theme.
        public async Task<UserPreferencesDto?> SyncFromServerAsync()
        {
            var profile = await profileService.GetCurrentAsync();
            if (profile?.Preferences != null)
            {
                await ApplyLocalCoreAsync(profile.Preferences.Theme);
                Changed?.Invoke(profile.Preferences.Theme);
                return profile.Preferences;
            }
            return null;
        }

        // Server sync (overload): used when the caller already has the prefs
        // (e.g. Profile.razor reusing its own UserProfileService fetch).
        // UserProfileService owns the cache now — this just applies locally
        // and notifies subscribers. The cache is already warm because
        // Profile's GetCurrentAsync populated it.
        public async Task SyncFromServerAsync(UserPreferencesDto serverPrefs)
        {
            await ApplyLocalCoreAsync(serverPrefs.Theme);
            Changed?.Invoke(serverPrefs.Theme);
        }

        // Local-only update: applies the theme to localStorage + <html data-theme>
        // without hitting the server. Used by Profile's revert path so a
        // post-PUT-failure rollback doesn't trigger another PUT (which can
        // also fail and double-throw).
        //
        // Also raises Changed so any subscribed component (Topbar today,
        // anything future) sees the rolled-back value. The originating
        // component's own handler short-circuits on its busy-flag so this
        // doesn't double-render.
        public async Task ApplyLocalAsync(ThemePreference value)
        {
            await ApplyLocalCoreAsync(value);
            Changed?.Invoke(value);
        }

        // Toggle path: optimistic local update (instant UI), then PUT.
        // On PUT failure the caller is responsible for reverting via
        // ApplyLocalAsync(prev) — NOT via SetAsync(prev), which would PUT
        // again and can throw a second uncaught exception.
        public async Task SetAsync(ThemePreference value, UserPreferencesDto fullPrefs)
        {
            await ApplyLocalCoreAsync(value);
            Changed?.Invoke(value);

            var client = clientFactory.CreateClient(Constants.API.ClientName);
            var dto = new UpdateUserPreferencesDto
            {
                Theme = value,
                WeeklyGoalHours = fullPrefs.WeeklyGoalHours,
                WorkdayHoursPerDay = fullPrefs.WorkdayHoursPerDay,
            };
            var response = await client.PutAsJsonAsync("User/me/preferences", dto);
            response.EnsureSuccessStatusCode();
            await profileService.InvalidateAsync();
        }

        // Convenience for callers that don't already have UserPreferencesDto
        // in hand (e.g. the topbar quick-toggle button). Awaits the cached
        // sync Task to recover current weeklyGoal/workdayHours, then PUTs the
        // full DTO with the new theme. Throws on PUT failure — caller reverts
        // via ApplyLocalAsync.
        public async Task SetThemeOnlyAsync(ThemePreference value)
        {
            var prefs = await SyncFromServerAsync();
            if (prefs == null)
            {
                // No prefs available (anonymous, network error). Apply locally
                // so the UI feels responsive; the next authenticated GetMe
                // will re-sync from server.
                await ApplyLocalCoreAsync(value);
                Changed?.Invoke(value);
                return;
            }
            await SetAsync(value, prefs);
        }

        private async Task ApplyLocalCoreAsync(ThemePreference value)
        {
            try
            {
                await js.InvokeVoidAsync("__theme.set", value.ToString());
            }
            catch (JSException) { /* bootstrap script absent — non-fatal */ }
            catch (JSDisconnectedException) { /* circuit gone */ }
        }

        // Register a callback so theme-bootstrap.js can notify us when
        // the OS color scheme changes mid-session for a 'System' user.
        // Idempotent — only the first call hits JS; later calls are no-ops.
        public async Task RegisterOsChangeListenerAsync()
        {
            if (selfRef != null) return;
            selfRef = DotNetObjectReference.Create(this);
            try { await js.InvokeVoidAsync("__theme.register", selfRef); }
            catch (JSException) { selfRef.Dispose(); selfRef = null; /* bootstrap absent */ }
            catch (JSDisconnectedException) { selfRef.Dispose(); selfRef = null; /* circuit gone */ }
        }

        // Invoked from theme-bootstrap.js when the OS color scheme changes
        // AND the user's stored preference is 'System'. The JS bootstrap
        // has already updated <html data-theme>; we just fire Changed so
        // Topbar re-renders its sun/moon icon.
        [JSInvokable]
        public Task NotifyResolvedThemeChangedAsync()
        {
            Changed?.Invoke(ThemePreference.System);
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (selfRef != null)
            {
                try { await js.InvokeVoidAsync("__theme.unregister"); }
                catch (JSException) { /* bootstrap absent — non-fatal */ }
                catch (JSDisconnectedException) { /* circuit gone */ }
            }
            selfRef?.Dispose();
            selfRef = null;
        }

        // Sync fallback for paths that go through IDisposable rather than
        // IAsyncDisposable. Cannot call JS here (would block or throw on
        // a disconnected circuit), so the JS-side dotnetRef stays attached
        // until the next page load. DisposeAsync above is the preferred path.
        public void Dispose()
        {
            selfRef?.Dispose();
            selfRef = null;
        }
    }
}
