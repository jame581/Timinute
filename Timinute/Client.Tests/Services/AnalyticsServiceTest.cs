using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Timinute.Client.Services;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos.Analytics;
using Xunit;

namespace Timinute.Client.Tests.Services
{
    public class AnalyticsServiceTest
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
        private static readonly DateTimeOffset From = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset To = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task GetSummaryAsync_Requests_Analytics_Summary()
        {
            Uri? requested = null;
            var service = CreateService(req =>
            {
                requested = req.RequestUri;
                return Task.FromResult(OkJson(new AnalyticsSummaryDto { TaskCount = 3 }));
            }, out _);

            var summary = await service.GetSummaryAsync(From, To);

            Assert.NotNull(summary);
            Assert.Equal(3, summary!.TaskCount);
            Assert.Equal("/Analytics/summary", requested!.AbsolutePath);
            Assert.Contains("TzOffsetMinutes=", requested.Query);
        }

        [Fact]
        public async Task Same_Range_Second_Call_Served_From_Cache()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(new AnalyticsSummaryDto())), out var handler);

            await service.GetSummaryAsync(From, To);
            await service.GetSummaryAsync(From, To);

            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task Same_Minute_Different_Seconds_Served_From_Cache()
        {
            // `to` is typically DateTimeOffset.Now, which changes every call at
            // tick precision — the cache key must truncate to the minute so
            // near-identical open-ended ranges still hit.
            var service = CreateService(_ => Task.FromResult(OkJson(new AnalyticsSummaryDto())), out var handler);
            var baseTo = new DateTimeOffset(2026, 7, 12, 10, 30, 0, TimeSpan.Zero);

            await service.GetSummaryAsync(From, baseTo);
            await service.GetSummaryAsync(From, baseTo.AddSeconds(10));

            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task Different_Range_Refetches()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(new AnalyticsSummaryDto())), out var handler);

            await service.GetSummaryAsync(From, To);
            await service.GetSummaryAsync(From.AddDays(-7), To);

            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task Invalidate_Clears_Cache_And_Raises_Event()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(new AnalyticsSummaryDto())), out var handler);
            var raised = false;
            service.Invalidated += () => raised = true;

            await service.GetSummaryAsync(From, To);
            service.Invalidate();
            await service.GetSummaryAsync(From, To);

            Assert.True(raised);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task Failed_Fetch_Returns_Null_And_Is_Not_Cached()
        {
            var fail = true;
            var service = CreateService(_ =>
            {
                if (fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                return Task.FromResult(OkJson(new AnalyticsSummaryDto { TaskCount = 1 }));
            }, out var handler);

            var first = await service.GetSummaryAsync(From, To);
            fail = false;
            var second = await service.GetSummaryAsync(From, To);

            Assert.Null(first);
            Assert.NotNull(second);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task InvalidationHandler_Clears_Cache_On_Successful_Post()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(new AnalyticsSummaryDto())), out var handler);
            await service.GetSummaryAsync(From, To);

            var invalidator = new AnalyticsCacheInvalidationHandler(service)
            {
                InnerHandler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
            };
            var mutationClient = new HttpClient(invalidator) { BaseAddress = new Uri("https://localhost/") };
            await mutationClient.PostAsync("TrackedTask", null);

            await service.GetSummaryAsync(From, To);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task InvalidationHandler_Ignores_Get_Requests()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(new AnalyticsSummaryDto())), out var handler);
            await service.GetSummaryAsync(From, To);

            var invalidator = new AnalyticsCacheInvalidationHandler(service)
            {
                InnerHandler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
            };
            var getClient = new HttpClient(invalidator) { BaseAddress = new Uri("https://localhost/") };
            await getClient.GetAsync("TrackedTask");

            await service.GetSummaryAsync(From, To);
            Assert.Equal(1, handler.CallCount);
        }

        private static AnalyticsService CreateService(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> responder,
            out StubHttpMessageHandler handler)
        {
            handler = new StubHttpMessageHandler(responder);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            return new AnalyticsService(factory.Object);
        }

        private static HttpResponseMessage OkJson<T>(T payload) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload, options: WebJson)
        };
    }
}
