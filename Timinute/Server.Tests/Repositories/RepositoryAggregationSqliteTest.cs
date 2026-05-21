using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    // Regression guard for the GET /User/me 500 found in the PR #44 smoke.
    //
    // BaseRepository.SumAsync(t => t.Duration.Ticks, ...) cannot be issued as
    // a server-side SQL SUM: TimeSpan.Ticks has no SQL aggregate translation,
    // and aggregates cannot be client-evaluated. The EF InMemory provider
    // hides this — it evaluates all LINQ in memory — so RepositoryAggregationTest
    // passed while production 500'd. These tests run against SQLite, a real
    // relational provider with the same translation rules as SQL Server, so
    // the failure is reproduced (and the fix verified) at the unit level.
    public class RepositoryAggregationSqliteTest : IAsyncLifetime
    {
        // Must equal ApplicationDbContext.SeedUserId1 (a private const there).
        // That HasData-seeded user owns 3 tracked tasks totaling 2 + 3 + 4 = 9h.
        // If the two ever drift apart, the filtered tests below match zero rows
        // and fail loudly — they do not pass vacuously.
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        private SqliteConnection _connection = null!;
        private ApplicationDbContext _context = null!;

        public async Task InitializeAsync()
        {
            // An in-memory SQLite database lives only as long as its connection.
            _connection = new SqliteConnection("DataSource=:memory:");
            await _connection.OpenAsync();
            _context = await TestHelper.GetSqliteApplicationDbContext(_connection);
        }

        public Task DisposeAsync()
        {
            _context.Dispose();
            _connection.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task SumAsync_DurationTicks_WorksAgainstRelationalProvider()
        {
            var repo = new BaseRepository<TrackedTask>(_context);

            // Compute the expected value by materializing first: a
            // .SumAsync(t => t.Duration.Ticks) query cannot be translated on a
            // relational provider, so such a query can't serve as the yardstick.
            var expectedTicks = (await _context.TrackedTasks
                    .Where(t => t.UserId == SeedUserId1)
                    .ToListAsync())
                .Sum(t => t.Duration.Ticks);

            var actualTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == SeedUserId1);

            Assert.Equal(expectedTicks, actualTicks);
            Assert.Equal(TimeSpan.FromHours(9).Ticks, actualTicks);
        }

        [Fact]
        public async Task SumAsync_EmptySet_ReturnsZero()
        {
            var repo = new BaseRepository<TrackedTask>(_context);

            var totalTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "no-such-user");

            Assert.Equal(0L, totalTicks);
        }

        [Fact]
        public async Task CountAsync_WithFilter_WorksAgainstRelationalProvider()
        {
            var repo = new BaseRepository<TrackedTask>(_context);

            var count = await repo.CountAsync(t => t.UserId == SeedUserId1);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task SumAsync_ExcludesSoftDeletedRows()
        {
            var repo = new BaseRepository<TrackedTask>(_context);

            var before = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == SeedUserId1);

            // Soft-delete one of SeedUserId1's tasks. The EF global query filter
            // must drop it from the aggregate — the IRepository doc promises
            // SumAsync honors that filter, and this verifies it against a real
            // relational provider (RepositoryAggregationTest covers only COUNT).
            var victimId = await _context.TrackedTasks
                .Where(t => t.UserId == SeedUserId1)
                .Select(t => t.TaskId)
                .FirstAsync();
            var victimTicks = (await _context.TrackedTasks
                .Where(t => t.TaskId == victimId)
                .Select(t => t.Duration)
                .SingleAsync()).Ticks;
            await repo.SoftDelete(victimId);

            var after = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == SeedUserId1);

            Assert.True(victimTicks > 0, "the soft-deleted task must have a non-zero duration for this test to mean anything");
            Assert.Equal(before - victimTicks, after);
        }
    }
}
