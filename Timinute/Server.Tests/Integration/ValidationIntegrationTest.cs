using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Shared.Dtos;
using Timinute.Shared.Dtos.Tag;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Exercises the [ApiController] auto-422 short-circuit through the real
    // pipeline (routing → auth → model binding → InvalidModelStateResponseFactory).
    // The pre-existing controller tests inject ModelState errors directly and
    // never hit this path (Copilot review PR #40 #6/#7).
    [Collection("Integration")]
    public class ValidationIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private readonly HttpClient client;

        public ValidationIntegrationTest(TiminuteApiFactory factory)
        {
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        [Fact]
        public async Task Create_Tag_With_Empty_Name_Returns_422_ProblemJson()
        {
            var response = await client.PostAsJsonAsync("/Tag", new CreateTagDto { Name = "", Color = "#6366F1" });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("errors", body);
            Assert.Contains("traceId", body);
        }

        [Fact]
        public async Task Create_TrackedTask_With_Default_StartDate_Returns_422()
        {
            var dto = new CreateTrackedTaskDto
            {
                Name = "Task",
                StartDate = default,
                Duration = TimeSpan.FromHours(1)
            };

            var response = await client.PostAsJsonAsync("/TrackedTask", dto);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task Analytics_Summary_With_From_After_To_Returns_422()
        {
            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-3).ToString("o"));

            var response = await client.GetAsync($"/Analytics/summary?From={from}&To={to}&TzOffsetMinutes=0");

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }

        [Fact]
        public async Task Analytics_Summary_With_Valid_Range_Returns_200()
        {
            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("o"));
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));

            var response = await client.GetAsync($"/Analytics/summary?From={from}&To={to}&TzOffsetMinutes=0");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Timer-notification preferences (v2.5). Sent as raw JSON so a field can be omitted.
        private static object Prefs(object longHours, object? idleMinutes, bool includeIdle = true) => includeIdle
            ? new { theme = "Dark", weeklyGoalHours = 32.0m, workdayHoursPerDay = 8.0m, longTimerWarningHours = longHours, idleWarningMinutes = idleMinutes }
            : new { theme = "Dark", weeklyGoalHours = 32.0m, workdayHoursPerDay = 8.0m, longTimerWarningHours = longHours };

        [Fact]
        public async Task Put_Preferences_LongTimer_25_Returns_422()
        {
            var response = await client.PutAsJsonAsync("/User/me/preferences", Prefs(25.0m, 15));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("LongTimerWarningHours", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Put_Preferences_Idle_Negative_Returns_422()
        {
            var response = await client.PutAsJsonAsync("/User/me/preferences", Prefs(8.0m, -1));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("IdleWarningMinutes", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Put_Preferences_Missing_IdleField_Returns_422()
        {
            var response = await client.PutAsJsonAsync("/User/me/preferences", Prefs(8.0m, null, includeIdle: false));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("IdleWarningMinutes", await response.Content.ReadAsStringAsync());
        }
    }
}
