using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Services.App;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Analytics;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    /// <summary>
    /// Proves the extracted app-services reproduce the controllers' ownership /
    /// validation behavior. The unchanged controller test suite is the primary
    /// no-drift proof; these tests pin the userId-parameterised service surface
    /// that the Task 7 MCP tools consume directly.
    /// </summary>
    public class AppServiceParityTest
    {
        // A HasData-seeded user that exists in the SQLite schema; the Project.UserId FK
        // is enforced there (unlike InMemory), so SQLite-backed tests must reference it.
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        // === ProjectAppService ===

        [Fact]
        public async Task ProjectAppService_Create_Assigns_Owner_And_Returns_Dto()
        {
            var (factory, mapper) = TestBench.NewProjectStack();
            var svc = new ProjectAppService(factory, mapper);

            var dto = await svc.CreateAsync("user-1", new CreateProjectDto { Name = "Website" });

            Assert.Equal("Website", dto.Name);
            Assert.False(string.IsNullOrWhiteSpace(dto.Color)); // palette default applied

            var list = await svc.ListAsync("user-1");
            Assert.Contains(list, p => p.Name == "Website");
            Assert.Empty(await svc.ListAsync("user-2")); // ownership isolation
        }

        [Fact]
        public async Task ProjectAppService_Create_Honors_Provided_Color()
        {
            var (factory, mapper) = TestBench.NewProjectStack();
            var svc = new ProjectAppService(factory, mapper);

            var dto = await svc.CreateAsync("user-1", new CreateProjectDto { Name = "Colored", Color = "#ABCDEF" });

            Assert.Equal("#ABCDEF", dto.Color);
        }

        // R7: the filtered unique index on (UserId, Name) is only enforced by a real
        // relational provider — InMemory ignores it — so the duplicate-name parity
        // test MUST run on SQLite.
        [Fact]
        public async Task ProjectAppService_Duplicate_Name_Throws_On_Sqlite()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var context = await TestHelper.GetSqliteApplicationDbContext(connection);
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new ProjectAppService(factory, mapper);

            await svc.CreateAsync(SeedUserId1, new CreateProjectDto { Name = "Dup" });

            await Assert.ThrowsAsync<ProjectNameConflictException>(
                () => svc.CreateAsync(SeedUserId1, new CreateProjectDto { Name = "Dup" }));
        }

        // R-validation: the MCP tools construct DTOs directly and bypass the [ApiController]
        // 422 short-circuit, so the app-service is the single choke point that enforces the
        // DTO Data Annotations. These pin that enforcement at the service surface.
        [Fact]
        public async Task ProjectAppService_Create_Rejects_Oversize_Name()
        {
            var (factory, mapper) = TestBench.NewProjectStack();
            var svc = new ProjectAppService(factory, mapper);

            var ex = await Assert.ThrowsAsync<AppValidationException>(
                () => svc.CreateAsync("user-1", new CreateProjectDto { Name = new string('x', 101) }));

            Assert.Contains("Project name must be between 2 and 100 characters.", ex.Message);
            Assert.Empty(await svc.ListAsync("user-1")); // nothing was persisted
        }

        [Fact]
        public async Task ProjectAppService_Create_Rejects_Bad_Color()
        {
            var (factory, mapper) = TestBench.NewProjectStack();
            var svc = new ProjectAppService(factory, mapper);

            var ex = await Assert.ThrowsAsync<AppValidationException>(
                () => svc.CreateAsync("user-1", new CreateProjectDto { Name = "Valid", Color = "not-a-color" }));

            Assert.Contains("Color must be a hex color in the form #RRGGBB.", ex.Message);
            Assert.Empty(await svc.ListAsync("user-1"));
        }

        // === TimeEntryAppService ===

        [Fact]
        public async Task TimeEntryAppService_Log_Rejects_MinDuration_Violation()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_MinDuration");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            var ex = await Assert.ThrowsAsync<AppValidationException>(() => svc.LogAsync("ApplicationUser1", new CreateTrackedTaskDto
            {
                Name = "Zero duration",
                StartDate = new DateTimeOffset(2022, 5, 1, 9, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.Zero
            }));

            Assert.Contains("Duration must be greater than zero.", ex.Message);
        }

        [Fact]
        public async Task TimeEntryAppService_Log_Rejects_Oversize_Name()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_OversizeName");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            var ex = await Assert.ThrowsAsync<AppValidationException>(() => svc.LogAsync("ApplicationUser1", new CreateTrackedTaskDto
            {
                Name = new string('x', 51),
                StartDate = new DateTimeOffset(2022, 5, 1, 9, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.FromHours(1)
            }));

            Assert.Contains("Name of task must be between 2 and 50 characters long.", ex.Message);
        }

        [Fact]
        public async Task TimeEntryAppService_Update_Rejects_Oversize_Name()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_UpdateOversize");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            var ex = await Assert.ThrowsAsync<AppValidationException>(() => svc.UpdateAsync("ApplicationUser1", "TrackedTaskId1", new UpdateTrackedTaskDto
            {
                TaskId = "TrackedTaskId1",
                Name = new string('x', 51),
                StartDate = new DateTimeOffset(2022, 5, 1, 9, 0, 0, TimeSpan.Zero)
            }));

            Assert.Contains("Name of task must be between 2 and 50 characters long.", ex.Message);
        }


        [Fact]
        public async Task TimeEntryAppService_Log_Scopes_To_User_And_Persists()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_Log");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            var dto = await svc.LogAsync("ApplicationUser1", new CreateTrackedTaskDto
            {
                Name = "Logged via service",
                StartDate = new DateTimeOffset(2022, 5, 1, 9, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.FromHours(2),
                ProjectId = "ProjectId1"
            });

            Assert.Equal("Logged via service", dto.Name);
            Assert.Equal("ProjectId1", dto.ProjectId);
            Assert.Equal(TimeSpan.FromHours(2), dto.Duration);
            Assert.Equal(dto.StartDate + dto.Duration, dto.EndDate);

            var persisted = await context.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == dto.TaskId);
            Assert.NotNull(persisted);
            Assert.Equal("ApplicationUser1", persisted!.UserId);
        }

        [Fact]
        public async Task TimeEntryAppService_Log_Foreign_Project_Throws()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_ForeignProject");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            // ProjectId2 belongs to ApplicationUser2 — logging it for ApplicationUser1 must fail.
            await Assert.ThrowsAsync<ProjectOwnershipException>(() => svc.LogAsync("ApplicationUser1", new CreateTrackedTaskDto
            {
                Name = "Foreign project",
                StartDate = new DateTimeOffset(2022, 5, 1, 9, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.FromHours(1),
                ProjectId = "ProjectId2"
            }));
        }

        [Fact]
        public async Task TimeEntryAppService_Search_Filters_By_Owner_And_Project()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_Search");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            var results = await svc.SearchAsync("ApplicationUser1", new TimeEntryQuery { ProjectId = "ProjectId1" });

            Assert.NotEmpty(results);
            Assert.All(results, t => Assert.Equal("ProjectId1", t.ProjectId));

            // Ownership isolation: a non-owner sees nothing.
            var none = await svc.SearchAsync("NonExistentUser", new TimeEntryQuery());
            Assert.Empty(none);
        }

        [Fact]
        public async Task TimeEntryAppService_Delete_Other_Users_Task_Returns_False()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_TE_Delete");
            var (factory, mapper) = TestBench.NewProjectStack(context);
            var svc = new TimeEntryAppService(factory, mapper, context);

            // TrackedTaskId5 belongs to ApplicationUser2.
            Assert.False(await svc.DeleteAsync("ApplicationUser1", "TrackedTaskId5"));

            // Owner can delete.
            Assert.True(await svc.DeleteAsync("ApplicationUser1", "TrackedTaskId1"));
        }

        // === AnalyticsAppService ===

        // Mirrors AnalyticsControllerTest.Get_Range_Summary_Test expectations exactly,
        // proving SummaryAsync reproduces the controller's in-memory aggregation.
        [Fact]
        public async Task AnalyticsAppService_Summary_Matches_Controller_Expectations()
        {
            var context = await TestHelper.GetDefaultApplicationDbContext("AppServiceParity_Analytics", analyticsTest: true);
            var svc = new AnalyticsAppService(context);

            var now = DateTimeOffset.UtcNow;
            var thisMonthFirst = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var from = thisMonthFirst.AddMonths(-1);
            var to = thisMonthFirst.AddSeconds(-1);

            var summary = await svc.SummaryAsync("ApplicationUser1000", from, to, 0);

            Assert.Equal(TimeSpan.FromHours(28), summary.TotalDuration);
            Assert.Equal(7, summary.TaskCount);
            Assert.Equal(1, summary.ActiveDays);
            Assert.Equal(TimeSpan.FromHours(28), summary.AveragePerActiveDay);
            Assert.True(summary.WeekdayCount >= 20);
            Assert.Equal(TimeSpan.FromHours((double)(8.0m * summary.WeekdayCount)), summary.TargetDuration);
        }
    }
}
