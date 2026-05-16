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

            // Seed total tracked tasks (all users combined) — see TestHelper.FillInitData
            var count = await repo.CountAsync();

            Assert.True(count > 0, "expected seed data to provide at least one tracked task");
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
    }
}
