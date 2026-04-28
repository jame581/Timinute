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
        public async Task SyncFromServerAsync()
        {
            try
            {
                var client = clientFactory.CreateClient(Constants.API.ClientName);
                var profile = await client.GetFromJsonAsync<UserProfileDto>("User/me");
                if (profile?.Preferences != null)
                {
                    await ApplyLocalAsync(profile.Preferences.Theme);
                    Changed?.Invoke(profile.Preferences.Theme);
                }
            }
            catch
            {
                // Anonymous, network error, or server hiccup — keep cache.
            }
        }

        // Server sync (overload): used when the caller already has the
        // server response, e.g. Profile.razor reusing its own GetMe.
        public async Task SyncFromServerAsync(UserPreferencesDto serverPrefs)
        {
            await ApplyLocalAsync(serverPrefs.Theme);
            Changed?.Invoke(serverPrefs.Theme);
        }

        // Toggle path: optimistic local update (instant UI), then PUT.
        // On PUT failure the caller is responsible for reverting via SetAsync(prev).
        public async Task SetAsync(ThemePreference value, UserPreferencesDto fullPrefs)
        {
            await ApplyLocalAsync(value);
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

        private async Task ApplyLocalAsync(ThemePreference value)
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
