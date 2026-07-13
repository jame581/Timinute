using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos.Analytics;

namespace Timinute.Client.Services
{
    // Session cache for the Analytics range endpoints, keyed by request URL.
    // Registered as a SINGLETON so AnalyticsCacheInvalidationHandler (created
    // by the HttpClientFactory in its own DI scope) shares this instance —
    // a scoped registration would silently give the handler a different one.
    public class AnalyticsService
    {
        private readonly IHttpClientFactory clientFactory;
        private readonly Dictionary<string, object> cache = new();

        public AnalyticsService(IHttpClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        public event Action? Invalidated;

        public Task<AnalyticsSummaryDto?> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<AnalyticsSummaryDto>("summary", from, to);

        public Task<List<DailyAnalyticsDto>?> GetDailyAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<List<DailyAnalyticsDto>>("daily", from, to);

        public Task<List<ProjectAnalyticsDto>?> GetProjectsAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<List<ProjectAnalyticsDto>>("projects", from, to);

        public Task<List<TagAnalyticsDto>?> GetTagsAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<List<TagAnalyticsDto>>("tags", from, to);

        public void Invalidate()
        {
            cache.Clear();
            Invalidated?.Invoke();
        }

        private async Task<T?> GetCachedAsync<T>(string endpoint, DateTimeOffset from, DateTimeOffset to) where T : class
        {
            var tz = (int)DateTimeOffset.Now.Offset.TotalMinutes;
            var url = $"{Constants.API.Analytics.Api}/{endpoint}" +
                      $"?From={Uri.EscapeDataString(from.ToString("o"))}" +
                      $"&To={Uri.EscapeDataString(to.ToString("o"))}" +
                      $"&TzOffsetMinutes={tz}";

            if (cache.TryGetValue(url, out var hit))
            {
                return (T)hit;
            }

            try
            {
                var client = clientFactory.CreateClient(Constants.API.ClientName);
                var result = await client.GetFromJsonAsync<T>(url);
                if (result != null)
                {
                    cache[url] = result;
                }
                return result;
            }
            catch
            {
                // Unauthenticated / network / server error — never cache failures.
                return null;
            }
        }
    }
}
