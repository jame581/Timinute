using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Models.Paging;
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

        [Fact]
        public async Task GetPaged_WithCollectionInclude_UsesStablePagingAndCount()
        {
            var repo = new BaseRepository<TrackedTask>(_context);

            var userTasks = await _context.TrackedTasks
                .AsTracking()
                .Where(t => t.UserId == SeedUserId1)
                .OrderBy(t => t.TaskId)
                .Take(2)
                .ToListAsync();

            var tagA = new Tag { TagId = "tag-sqlite-a", Name = "tag-sqlite-a", Color = "#111111", UserId = SeedUserId1 };
            var tagB = new Tag { TagId = "tag-sqlite-b", Name = "tag-sqlite-b", Color = "#222222", UserId = SeedUserId1 };
            _context.Tags.AddRange(tagA, tagB);

            userTasks[0].Tags = new List<Tag> { tagA, tagB };
            userTasks[1].Tags = new List<Tag> { tagA };
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var page = await repo.GetPaged(
                new PagingParameters { PageNumber = 1, PageSize = 2 },
                t => t.UserId == SeedUserId1,
                orderBy: nameof(TrackedTask.TaskId),
                includeProperties: nameof(TrackedTask.Tags));

            var ids = page.Select(t => t.TaskId).ToList();

            Assert.Equal(3, page.TotalCount);
            Assert.Equal(2, page.Count);
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(page, task => Assert.NotNull(task.Tags));
        }
    }
}
