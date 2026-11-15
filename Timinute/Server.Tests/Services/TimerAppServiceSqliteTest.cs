using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Services.App;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    // SQLite, not InMemory: the stop path relies on a real transaction and a real PK,
    // both of which InMemory silently ignores.
    public class TimerAppServiceSqliteTest : IAsyncLifetime
    {
        private const string User1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";
        private const string User2 = "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e";
        private const string OwnProjectId = "timer-own-project";
        private const string ForeignProjectId = "timer-foreign-project";

        private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        private SqliteConnection connection = null!;
        private ApplicationDbContext db = null!;
        private FakeTimeProvider clock = null!;

        public async Task InitializeAsync()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            db = await TestHelper.GetSqliteApplicationDbContext(connection);
            db.Projects.AddRange(
                new Project { ProjectId = OwnProjectId, Name = "Timer own", Color = "#112233", UserId = User1 },
                new Project { ProjectId = ForeignProjectId, Name = "Timer foreign", Color = "#445566", UserId = User2 });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            clock = new FakeTimeProvider(T0);
        }

        public async Task DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }

        private TimerAppService NewService(ITimeEntryAppService? timeEntries = null)
        {
            var (factory, mapper) = TestBench.NewProjectStack(db);
            timeEntries ??= new TimeEntryAppService(factory, mapper, db);
            return new TimerAppService(db, timeEntries, mapper, clock);
        }

        [Fact]
        public async Task Start_Then_Get_Returns_Timer_With_ServerStartDate()
        {
            var svc = NewService();

            var started = await svc.StartAsync(User1, new StartTimerDto { Name = "  Write spec  ", ProjectId = OwnProjectId });
            var got = await svc.GetAsync(User1);

            Assert.Equal(T0, started.StartDate);
            Assert.NotNull(got);
            Assert.Equal("Write spec", got!.Name);
            Assert.Equal(T0, got.StartDate);
            Assert.Equal(OwnProjectId, got.ProjectId);
            Assert.Equal("Timer own", got.ProjectName);
            Assert.Equal("#112233", got.ProjectColor);
        }

        [Fact]
        public async Task Get_NoTimer_ReturnsNull()
        {
            Assert.Null(await NewService().GetAsync(User1));
        }

        [Fact]
        public async Task Start_WhenRunning_ThrowsConflict()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "First" });

            await Assert.ThrowsAsync<TimerConflictException>(() => svc.StartAsync(User1, new StartTimerDto { Name = "Second" }));
        }

        [Fact]
        public async Task Start_ConcurrentInsert_PkViolation_Maps_To_Conflict()
        {
            var svc = NewService();
            // Simulates a second tab winning the race between the pre-check and the insert.
            svc.BeforeInsertForTests = async () =>
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
                await using var other = new ApplicationDbContext(options);
                other.RunningTimers.Add(new RunningTimer { UserId = User1, Name = "Other tab", StartDate = T0 });
                await other.SaveChangesAsync();
            };

            await Assert.ThrowsAsync<TimerConflictException>(() => svc.StartAsync(User1, new StartTimerDto { Name = "This tab" }));
        }

        [Fact]
        public async Task Start_InvalidName_ThrowsValidation()
        {
            await Assert.ThrowsAsync<AppValidationException>(() => NewService().StartAsync(User1, new StartTimerDto { Name = "x" }));
        }

        [Fact]
        public async Task Start_ForeignProject_ThrowsProjectOwnership()
        {
            await Assert.ThrowsAsync<ProjectOwnershipException>(
                () => NewService().StartAsync(User1, new StartTimerDto { Name = "Sneaky", ProjectId = ForeignProjectId }));
            Assert.False(await db.RunningTimers.AnyAsync());
        }

        [Fact]
        public async Task Start_WhitespaceProject_StoresNull()
        {
            var started = await NewService().StartAsync(User1, new StartTimerDto { Name = "No project", ProjectId = "   " });

            Assert.Null(started.ProjectId);
            Assert.Null((await db.RunningTimers.SingleAsync()).ProjectId);
        }

        [Fact]
        public async Task Start_TrailingSpaceProject_IsTrimmed()
        {
            await NewService().StartAsync(User1, new StartTimerDto { Name = "Padded", ProjectId = OwnProjectId + " " });

            Assert.Equal(OwnProjectId, (await db.RunningTimers.SingleAsync()).ProjectId);
        }

        [Fact]
        public async Task Stop_Creates_One_TrackedTask_And_Removes_Timer()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Deep work", ProjectId = OwnProjectId });
            clock.Advance(TimeSpan.FromMinutes(90));

            var result = await svc.StopAsync(User1, new StopTimerDto());

            Assert.False(result.ProjectCleared);
            Assert.Equal(TimeSpan.FromMinutes(90), result.TrackedTask.Duration);
            Assert.Equal(T0, result.TrackedTask.StartDate);
            Assert.Equal(T0.AddMinutes(90), result.TrackedTask.EndDate);
            Assert.Equal(OwnProjectId, result.TrackedTask.ProjectId);
            Assert.Equal(1, await db.TrackedTasks.CountAsync(t => t.Name == "Deep work"));
            Assert.False(await db.RunningTimers.AnyAsync());
        }

        [Fact]
        public async Task Stop_WithEndAt_UsesIt()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Idle trimmed" });
            clock.Advance(TimeSpan.FromHours(2));

            var result = await svc.StopAsync(User1, new StopTimerDto { EndAt = T0.AddMinutes(45) });

            Assert.Equal(TimeSpan.FromMinutes(45), result.TrackedTask.Duration);
        }

        [Fact]
        public async Task Stop_EndAtBeforeStart_Returns422()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Backwards" });
            clock.Advance(TimeSpan.FromMinutes(10));

            await Assert.ThrowsAsync<InvalidTimeRangeException>(() => svc.StopAsync(User1, new StopTimerDto { EndAt = T0.AddMinutes(-1) }));
            Assert.True(await db.RunningTimers.AnyAsync());
        }

        [Fact]
        public async Task Stop_EndAtInFuture_BeyondSkew_Throws()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Future" });
            clock.Advance(TimeSpan.FromMinutes(10));

            await Assert.ThrowsAsync<InvalidTimeRangeException>(
                () => svc.StopAsync(User1, new StopTimerDto { EndAt = clock.GetUtcNow().AddSeconds(30) }));
        }

        [Fact]
        public async Task Stop_UnderOneSecond_ClampsToOneSecond()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Blink" });

            var result = await svc.StopAsync(User1, new StopTimerDto());

            Assert.Equal(TimeSpan.FromSeconds(1), result.TrackedTask.Duration);
        }

        [Fact]
        public async Task Stop_ProjectSoftDeletedMidRun_SavesWithoutProject()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Orphaned", ProjectId = OwnProjectId });
            var project = await db.Projects.AsTracking().SingleAsync(p => p.ProjectId == OwnProjectId);
            project.DeletedAt = T0;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            clock.Advance(TimeSpan.FromMinutes(5));

            var result = await svc.StopAsync(User1, new StopTimerDto());

            Assert.True(result.ProjectCleared);
            Assert.Null(result.TrackedTask.ProjectId);
            Assert.False(await db.RunningTimers.AnyAsync());
        }

        [Fact]
        public async Task Stop_WhenLogFails_TimerRowSurvives()
        {
            var failing = new Mock<ITimeEntryAppService>();
            failing.Setup(s => s.LogAsync(It.IsAny<string>(), It.IsAny<CreateTrackedTaskDto>()))
                   .ThrowsAsync(new InvalidOperationException("boom"));
            await NewService().StartAsync(User1, new StartTimerDto { Name = "Fragile" });
            clock.Advance(TimeSpan.FromMinutes(5));

            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(failing.Object).StopAsync(User1, new StopTimerDto()));

            db.ChangeTracker.Clear();
            Assert.True(await db.RunningTimers.AnyAsync(t => t.UserId == User1));
        }

        [Fact]
        public async Task Stop_DeleteFailsAfterLog_RollsBackTask_AndReportsNotFound()
        {
            // LogAsync saves the TrackedTask, then a competing stop (simulated by raw SQL inside the
            // same transaction) removes the timer row, so our delete hits 0 rows. The transaction must
            // roll the task back, and the caller gets "not found" rather than a 500.
            var (factory, mapper) = TestBench.NewProjectStack(db);
            var real = new TimeEntryAppService(factory, mapper, db);
            var racing = new Mock<ITimeEntryAppService>();
            racing.Setup(s => s.LogAsync(It.IsAny<string>(), It.IsAny<CreateTrackedTaskDto>()))
                  .Returns(async (string userId, CreateTrackedTaskDto dto) =>
                  {
                      var created = await real.LogAsync(userId, dto);
                      await db.Database.ExecuteSqlRawAsync("DELETE FROM RunningTimers");
                      return created;
                  });
            await NewService().StartAsync(User1, new StartTimerDto { Name = "Raced stop" });
            clock.Advance(TimeSpan.FromMinutes(5));

            await Assert.ThrowsAsync<TimerNotFoundException>(() => NewService(racing.Object).StopAsync(User1, new StopTimerDto()));

            db.ChangeTracker.Clear();
            Assert.False(await db.TrackedTasks.AnyAsync(t => t.Name == "Raced stop"));
        }

        [Fact]
        public async Task Stop_ProjectTrashedBetweenChecks_SavesWithoutProject()
        {
            var created = new TrackedTaskDto { Name = "Late trash" };
            var entries = new Mock<ITimeEntryAppService>();
            entries.SetupSequence(s => s.LogAsync(User1, It.IsAny<CreateTrackedTaskDto>()))
                   .ThrowsAsync(new ProjectOwnershipException())
                   .ReturnsAsync(created);
            await NewService().StartAsync(User1, new StartTimerDto { Name = "Late trash", ProjectId = OwnProjectId });

            var result = await NewService(entries.Object).StopAsync(User1, new StopTimerDto());

            Assert.True(result.ProjectCleared);
            entries.Verify(s => s.LogAsync(User1, It.Is<CreateTrackedTaskDto>(d => d.ProjectId == null)), Times.Once);
            db.ChangeTracker.Clear();
            Assert.False(await db.RunningTimers.AnyAsync());
        }

        [Fact]
        public async Task Stop_NoTimer_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<TimerNotFoundException>(() => NewService().StopAsync(User1, new StopTimerDto()));
        }

        [Fact]
        public async Task Discard_Removes_Without_Task()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Never mind" });
            var before = await db.TrackedTasks.CountAsync();

            await svc.DiscardAsync(User1);

            Assert.False(await db.RunningTimers.AnyAsync());
            Assert.Equal(before, await db.TrackedTasks.CountAsync());
        }

        [Fact]
        public async Task Discard_NoTimer_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<TimerNotFoundException>(() => NewService().DiscardAsync(User1));
        }

        [Fact]
        public async Task OtherUser_CannotSee_Stop_Or_Discard()
        {
            var svc = NewService();
            await svc.StartAsync(User1, new StartTimerDto { Name = "Mine" });

            Assert.Null(await svc.GetAsync(User2));
            await Assert.ThrowsAsync<TimerNotFoundException>(() => svc.StopAsync(User2, new StopTimerDto()));
            await Assert.ThrowsAsync<TimerNotFoundException>(() => svc.DiscardAsync(User2));
            Assert.True(await db.RunningTimers.AnyAsync(t => t.UserId == User1));
        }
    }
}
