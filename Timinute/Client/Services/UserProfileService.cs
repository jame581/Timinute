using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos;

namespace Timinute.Client.Services
{
    // Caches GET /User/me for the duration of the WASM app session.
    // Concurrent callers share the same in-flight Task; call
    // InvalidateAsync after any server mutation that changes the
    // cached fields (e.g. preferences PUT).
    public class UserProfileService
    {
        private readonly IHttpClientFactory clientFactory;
        private Task<UserProfileDto?>? fetchTask;

        public UserProfileService(IHttpClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        // Reserved for future components that need to react to profile updates
        // (e.g. a nav-menu showing the user's name after a profile rename).
        // No subscribers today.
        public event Action<UserProfileDto?>? Changed;

        public Task<UserProfileDto?> GetCurrentAsync()
            => fetchTask ??= FetchAsync();

        public Task InvalidateAsync()
        {
            fetchTask = null;
            return Task.CompletedTask;
        }

        private async Task<UserProfileDto?> FetchAsync()
        {
            try
            {
                var client = clientFactory.CreateClient(Constants.API.ClientName);
                var profile = await client.GetFromJsonAsync<UserProfileDto>("User/me");
                Changed?.Invoke(profile);
                return profile;
            }
            catch
            {
                // Unauthenticated, network error, or server hiccup — keep
                // the cache empty so the next call retries.
                fetchTask = null;
                return null;
            }
        }
    }
}
