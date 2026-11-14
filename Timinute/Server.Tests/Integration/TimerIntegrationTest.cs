using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Timinute.Shared.Dtos.Analytics;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Real pipeline (routing -> auth -> binding -> controller -> service -> EF) for the timer surface.
    // The factory's DB is shared across the Integration collection, so each test starts by
    // discarding any timer a previous test left behind.
    [Collection("Integration")]
    public class TimerIntegrationTest : IClassFixture<TiminuteApiFactory>, IAsyncLifetime
    {
        private readonly HttpClient client;

        public TimerIntegrationTest(TiminuteApiFactory factory)
        {
            client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        }

        public async Task InitializeAsync() => await client.DeleteAsync("/Timer");

        public async Task DisposeAsync() => await client.DeleteAsync("/Timer");

        [Fact]
        public async Task Start_Stop_RoundTrip_Creates_Task_Visible_In_Search()
        {
            var name = "Roundtrip " + Guid.NewGuid().ToString("N")[..8];

            var start = await client.PostAsJsonAsync("/Timer/start", new StartTimerDto { Name = name });
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);

            var get = await client.GetFromJsonAsync<RunningTimerDto>("/Timer");
            Assert.Equal(name, get!.Name);

            var stop = await client.PostAsJsonAsync("/Timer/stop", new StopTimerDto());
            Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
            var result = await stop.Content.ReadFromJsonAsync<StopTimerResultDto>();
            Assert.Equal(name, result!.TrackedTask.Name);

            Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/Timer")).StatusCode);
            var found = await client.GetFromJsonAsync<TrackedTaskDto[]>($"/TrackedTask/search?search={name}");
            Assert.Single(found!);
        }

        [Fact]
        public async Task RunningTimer_NotIn_TrackedTask_Search_Analytics_Or_Export()
        {
            var name = "Invisible " + Guid.NewGuid().ToString("N")[..8];
            // Analytics caps a range at 400 days; a window around "now" covers where a running timer would land.
            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-30).ToString("o"));
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("o"));
            var summaryUrl = $"/Analytics/summary?from={from}&to={to}";
            var before = await client.GetFromJsonAsync<AnalyticsSummaryDto>(summaryUrl);

            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/Timer/start", new StartTimerDto { Name = name })).StatusCode);

            var found = await client.GetFromJsonAsync<TrackedTaskDto[]>($"/TrackedTask/search?search={name}");
            Assert.Empty(found!);
            var after = await client.GetFromJsonAsync<AnalyticsSummaryDto>(summaryUrl);
            Assert.Equal(before!.TaskCount, after!.TaskCount);
            var csv = await client.GetStringAsync("/Export/tasks?format=csv");
            Assert.DoesNotContain(name, csv);
        }

        [Fact]
        public async Task Start_Twice_Returns_409_ProblemJson()
        {
            await client.PostAsJsonAsync("/Timer/start", new StartTimerDto { Name = "First" });

            var second = await client.PostAsJsonAsync("/Timer/start", new StartTimerDto { Name = "Second" });

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal("application/problem+json", second.Content.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task Start_EmptyName_Returns_422_ProblemJson()
        {
            var response = await client.PostAsJsonAsync("/Timer/start", new StartTimerDto { Name = "" });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task Stop_EndAtBeforeStart_Returns_422()
        {
            await client.PostAsJsonAsync("/Timer/start", new StartTimerDto { Name = "Backwards" });

            var response = await client.PostAsJsonAsync("/Timer/stop", new StopTimerDto { EndAt = DateTimeOffset.UtcNow.AddHours(-1) });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
    }
}
