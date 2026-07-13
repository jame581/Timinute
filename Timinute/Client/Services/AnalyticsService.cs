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

        // Cache dictionary is small (bounded below) but never allowed to grow
        // unbounded across a long session.
        private const int MaxCacheEntries = 50;

        public Task<AnalyticsSummaryDto?> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<AnalyticsSummaryDto>(Constants.API.Analytics.Summary, from, to);

        public Task<List<DailyAnalyticsDto>?> GetDailyAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<List<DailyAnalyticsDto>>(Constants.API.Analytics.Daily, from, to);

        public Task<List<ProjectAnalyticsDto>?> GetProjectsAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<List<ProjectAnalyticsDto>>(Constants.API.Analytics.Projects, from, to);

        public Task<List<TagAnalyticsDto>?> GetTagsAsync(DateTimeOffset from, DateTimeOffset to)
            => GetCachedAsync<List<TagAnalyticsDto>>(Constants.API.Analytics.TagsBreakdown, from, to);

        public void Invalidate()
        {
            cache.Clear();
            Invalidated?.Invoke();
        }

        // Open-ended ranges pass DateTimeOffset.Now as `to`, which carries
        // sub-second precision that changes on every call — with tick-precision
        // "o" formatting in the cache key, that meant every call was a guaranteed
        // cache miss. Truncating both ends to whole minutes keeps the cache
        // effective (repeated calls within the same minute hit) while staying
        // fresh enough for analytics data that doesn't need finer granularity.
        private static DateTimeOffset TruncateToMinute(DateTimeOffset v)
            => new DateTimeOffset(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0, v.Offset);

        private async Task<T?> GetCachedAsync<T>(string endpoint, DateTimeOffset from, DateTimeOffset to) where T : class
        {
            var tz = (int)DateTimeOffset.Now.Offset.TotalMinutes;
            var truncatedFrom = TruncateToMinute(from);
            var truncatedTo = TruncateToMinute(to);
            var url = $"{endpoint}" +
                      $"?From={Uri.EscapeDataString(truncatedFrom.ToString("o"))}" +
                      $"&To={Uri.EscapeDataString(truncatedTo.ToString("o"))}" +
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
                    // Belt-and-braces: this cache is meant to stay small (per-session,
                    // a handful of distinct ranges); if something is churning it up,
                    // clear rather than let it grow unbounded.
                    if (cache.Count >= MaxCacheEntries)
                    {
                        cache.Clear();
                    }
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
