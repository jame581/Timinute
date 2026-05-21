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
        {
            // Reuse an in-flight fetch or a cached successful result. A fetch
            // that completed without a profile (unauthenticated / network
            // error) is dropped here so the next call retries.
            //
            // fetchTask is assigned ONLY in this method — never from
            // FetchAsync's continuation. An earlier version reset the field
            // inside FetchAsync's catch, which raced this assignment: the
            // continuation could null the field before `??=` had written it,
            // re-caching the failed task and permanently blocking retries.
            var cached = fetchTask;
            if (cached is not null && !HasFailed(cached))
                return cached;

            return fetchTask = FetchAsync();
        }

        public Task InvalidateAsync()
        {
            fetchTask = null;
            return Task.CompletedTask;
        }

        // A fetch counts as failed once it has completed faulted/cancelled or
        // with a null profile. Result is only read after the faulted/cancelled
        // checks short-circuit, so it never re-throws.
        private static bool HasFailed(Task<UserProfileDto?> task)
            => task.IsCompleted
               && (task.IsFaulted || task.IsCanceled || task.Result is null);

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
                // Unauthenticated, network error, or server hiccup. Return
                // null; GetCurrentAsync drops this task from the cache on the
                // next call. FetchAsync must NOT touch fetchTask itself —
                // that races GetCurrentAsync's own assignment of the field.
                return null;
            }
        }
    }
}
