using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Timinute.Client.Services
{
    // Every successful non-GET through the API client can change analytics
    // numbers (tasks, tags, projects, preferences) — drop the whole cache.
    // One registration replaces per-page Invalidate() calls at all mutation sites.
    public class AnalyticsCacheInvalidationHandler : DelegatingHandler
    {
        private readonly AnalyticsService analytics;

        public AnalyticsCacheInvalidationHandler(AnalyticsService analytics)
        {
            this.analytics = analytics;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (request.Method != HttpMethod.Get && response.IsSuccessStatusCode)
            {
                analytics.Invalidate();
            }

            return response;
        }
    }
}
