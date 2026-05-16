using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    public class RepositoryAggregationTest
    {
        private const string _databaseName = "RepositoryAggregation_Test_DB";

        [Fact]
        public async Task CountAsync_WithFilter_ReturnsMatchingCount()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CountFilter");
            var repo = new BaseRepository<TrackedTask>(db);

            // Seed has 4 tasks for ApplicationUser1 (TrackedTaskId1..4)
            var count = await repo.CountAsync(t => t.UserId == "ApplicationUser1");

            Assert.Equal(4, count);
        }

        [Fact]
        public async Task CountAsync_NoFilter_ReturnsTotal()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CountAll");
            var repo = new BaseRepository<TrackedTask>(db);

            // TestHelper.FillInitData seeds 8 tracked tasks across 3 users
            // (User1: 4, User2: 3, User3: 1). ApplicationDbContext.OnModelCreating
            // may also apply HasData seeding on top, so assert >= 8 rather than
            // == 8 to stay robust against the model-seed path being present or not.
            var count = await repo.CountAsync();

            Assert.True(count >= 8, $"expected at least 8 tracked tasks from FillInitData; got {count}");
        }

        [Fact]
        public async Task CountAsync_RespectsGlobalQueryFilter_ExcludesSoftDeleted()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CountSoftDeleted");
            var repo = new BaseRepository<TrackedTask>(db);

            // Soft-delete one of ApplicationUser1's tasks
            await repo.SoftDelete("TrackedTaskId1");

            var count = await repo.CountAsync(t => t.UserId == "ApplicationUser1");

            // 4 seeded - 1 soft-deleted = 3 visible through the global filter
            Assert.Equal(3, count);
        }

        [Fact]
        public async Task SumAsync_WithFilter_ReturnsFilteredSum()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SumFilter");
            var repo = new BaseRepository<TrackedTask>(db);

            // Compute expected by summing the same set client-side from the same context.
            var expectedTicks = await db.TrackedTasks
                .Where(t => t.UserId == "ApplicationUser1")
                .SumAsync(t => t.Duration.Ticks);

            var actualTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "ApplicationUser1");

            Assert.Equal(expectedTicks, actualTicks);
            Assert.True(actualTicks > 0, "expected non-zero seed durations");
        }

        [Fact]
        public async Task SumAsync_EmptySet_ReturnsZero()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SumEmpty");
            var repo = new BaseRepository<TrackedTask>(db);

            var totalTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "no-such-user");

            Assert.Equal(0L, totalTicks);
        }

        [Fact]
        public async Task SumAsync_RespectsGlobalQueryFilter_ExcludesSoftDeleted()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SumSoftDeleted");
            var repo = new BaseRepository<TrackedTask>(db);

            // Capture the full sum for ApplicationUser1, then soft-delete one
            // task and confirm the new sum drops by that task's duration.
            var fullSumTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "ApplicationUser1");
            var deletedTaskTicks = (await db.TrackedTasks.FindAsync("TrackedTaskId1"))!.Duration.Ticks;

            await repo.SoftDelete("TrackedTaskId1");

            var afterSumTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "ApplicationUser1");

            Assert.Equal(fullSumTicks - deletedTaskTicks, afterSumTicks);
        }
    }
}
