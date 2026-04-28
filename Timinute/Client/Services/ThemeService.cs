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
    public class ThemeService
    {
        private readonly IJSRuntime js;
        private readonly IHttpClientFactory clientFactory;

        // Holds the once-per-session sync Task so multiple callers (MainLayout,
        // Profile, Dashboard) don't all fire their own GET /User/me. First caller
        // populates; everyone else awaits the cached task. Profile's overload
        // (which already has the prefs) populates this with a completed task so
        // a later MainLayout call is a no-op.
        private Task<UserPreferencesDto?>? syncTask;

        public ThemeService(IJSRuntime js, IHttpClientFactory clientFactory)
        {
            this.js = js;
            this.clientFactory = clientFactory;
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

        // Server sync: pulls GetMe and writes the authenticated user's
        // preference into localStorage so the next reload's bootstrap reads
        // the up-to-date value. Silently no-ops if unauthenticated or the
        // call fails — the bootstrap script already applied a default theme.
        // Idempotent: subsequent callers await the cached task and don't
        // refire the network request.
        public Task<UserPreferencesDto?> SyncFromServerAsync()
        {
            return syncTask ??= FetchAndApplyAsync();
        }

        // Server sync (overload): used when the caller already has the
        // server response, e.g. Profile.razor reusing its own GetMe. Also
        // populates the cache so a subsequent parameterless call (e.g. from
        // MainLayout) doesn't fire a duplicate GET /User/me.
        public async Task SyncFromServerAsync(UserPreferencesDto serverPrefs)
        {
            syncTask ??= Task.FromResult<UserPreferencesDto?>(serverPrefs);
            await ApplyLocalAsync(serverPrefs.Theme);
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

        private async Task<UserPreferencesDto?> FetchAndApplyAsync()
        {
            try
            {
                var client = clientFactory.CreateClient(Constants.API.ClientName);
                var profile = await client.GetFromJsonAsync<UserProfileDto>("User/me");
                if (profile?.Preferences != null)
                {
                    await ApplyLocalCoreAsync(profile.Preferences.Theme);
                    Changed?.Invoke(profile.Preferences.Theme);
                    return profile.Preferences;
                }
            }
            catch
            {
                // Anonymous, network error, or server hiccup — keep cache.
                // Reset syncTask so a later, post-auth call can retry.
                syncTask = null;
            }
            return null;
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
    }
}
