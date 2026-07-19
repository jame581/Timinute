using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Shared.Dtos.Analytics;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Cross-feature end-to-end flows through the real HTTP pipeline (routing -> auth ->
    // model binding -> controller -> app-service -> EF). Per-feature tests cover each
    // controller in isolation; these prove the features integrate — data logged through
    // one endpoint is consistently visible through the others. TestAuthHandler authenticates
    // every request as ApplicationUser1 (a clean, non-seeded user), so the only rows in play
    // are the ones each test creates.
    [Collection("Integration")]
    public class CrossFeatureIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private readonly HttpClient client;

        public CrossFeatureIntegrationTest(TiminuteApiFactory factory)
        {
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        [Fact]
        public async Task LoggedTime_Is_Visible_In_Search_And_Reflected_In_Analytics()
        {
            // 1. Create a project.
            var project = await PostProject("E2E Tracking Journey", "#10B981");

            // 2. Log a 2-hour task under it on a distinctive date (keeps the analytics range
            //    clear of other tests' data), through the real create pipeline.
            var start = new DateTimeOffset(2025, 3, 15, 12, 0, 0, TimeSpan.Zero);
            var created = await PostTrackedTask(new CreateTrackedTaskDto
            {
                Name = "Write integration tests",
                StartDate = start,
                Duration = TimeSpan.FromHours(2),
                ProjectId = project.ProjectId
            });
            Assert.Equal(project.ProjectId, created.ProjectId);
            Assert.Equal(start.Add(TimeSpan.FromHours(2)), created.EndDate);

            // 3. Search scoped to the project finds exactly that task (project id is unique
            //    to this test, so the paged search result is unambiguous).
            var found = await client.GetFromJsonAsync<TrackedTaskDto[]>(
                $"/TrackedTask/search?projectId={project.ProjectId}&PageSize=100&PageNumber=1");
            Assert.NotNull(found);
            Assert.Single(found!);
            Assert.Equal(created.TaskId, found![0].TaskId);

            // 4. The analytics summary over a range covering that day reflects the 2 hours.
            var from = new DateTimeOffset(2025, 3, 14, 0, 0, 0, TimeSpan.Zero);
            var to = new DateTimeOffset(2025, 3, 16, 0, 0, 0, TimeSpan.Zero);
            var summary = await client.GetFromJsonAsync<AnalyticsSummaryDto>(
                $"/Analytics/summary?From={Iso(from)}&To={Iso(to)}&TzOffsetMinutes=0");
            Assert.NotNull(summary);
            Assert.True(summary!.TotalDuration >= TimeSpan.FromHours(2),
                $"analytics total {summary.TotalDuration} should include the logged 2h");
            Assert.True(summary.TaskCount >= 1);
            Assert.True(summary.ActiveDays >= 1);
        }

        [Fact]
        public async Task Deleting_A_Project_Cascades_To_Its_Tasks_And_Restore_Brings_Them_Back()
        {
            var project = await PostProject("E2E Cascade Project", "#6366F1");
            var task = await PostTrackedTask(new CreateTrackedTaskDto
            {
                Name = "Task under cascade project",
                StartDate = new DateTimeOffset(2025, 4, 2, 9, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.FromHours(1),
                ProjectId = project.ProjectId
            });

            // The task is retrievable before the delete.
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/TrackedTask/{task.TaskId}")).StatusCode);

            // Soft-deleting the project cascades the same DeletedAt to its active child tasks,
            // and the global query filter then hides the task from GetById.
            var delete = await client.DeleteAsync($"/Project/{project.ProjectId}");
            Assert.True(delete.IsSuccessStatusCode, $"project delete returned {(int)delete.StatusCode}");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/TrackedTask/{task.TaskId}")).StatusCode);

            // Restoring the project brings back only the tasks it cascade-deleted.
            var restore = await client.PostAsync($"/Project/{project.ProjectId}/restore", content: null);
            Assert.True(restore.IsSuccessStatusCode, $"project restore returned {(int)restore.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/TrackedTask/{task.TaskId}")).StatusCode);
        }

        private async Task<ProjectDto> PostProject(string name, string color)
        {
            var response = await client.PostAsJsonAsync("/Project", new CreateProjectDto { Name = name, Color = color });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
        }

        private async Task<TrackedTaskDto> PostTrackedTask(CreateTrackedTaskDto dto)
        {
            var response = await client.PostAsJsonAsync("/TrackedTask", dto);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<TrackedTaskDto>())!;
        }

        private static string Iso(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("o"));
    }
}
