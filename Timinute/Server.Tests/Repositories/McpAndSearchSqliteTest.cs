using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Services;
using Timinute.Server.Services.App;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    // Proves the two review-driven changes translate to real SQL. InMemory would
    // client-evaluate ToLowerInvariant() and silently accept ExecuteDeleteAsync being
    // unsupported; SQLite exercises the relational path and throws if either can't translate.
    public class McpAndSearchSqliteTest
    {
        // HasData seed user (ApplicationDbContext.SeedUserId1) owns "Project A/B/C".
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        [Fact]
        public async Task Search_Predicate_Translates_And_Matches_Case_Insensitively_On_Sqlite()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var db = await TestHelper.GetSqliteApplicationDbContext(connection);

            // Lower-cased query against the seeded "Project A" — forces ToLowerInvariant() to
            // translate to SQL; a translation failure throws instead of client-evaluating.
            var predicate = TimeEntryAppService.BuildSearchPredicate(
                SeedUserId1, from: null, to: null, projectId: null, search: "project a", tagIds: null);

            var matches = await db.TrackedTasks.Where(predicate).Select(t => t.Name).ToListAsync();

            Assert.Contains("Project A", matches);
            Assert.DoesNotContain("Project B", matches);
        }

        [Fact]
        public async Task PurgeOnce_Deletes_Via_Sql_On_Relational_Provider()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var db = await TestHelper.GetSqliteApplicationDbContext(connection);

            // UTC timestamps only — the SQLite DateTimeOffset binary converter orders correctly
            // for zero-offset values (see CLAUDE.md testing notes).
            db.McpActivityLogs.AddRange(
                new McpActivityLog { UserId = "u", Tool = "old", Timestamp = DateTimeOffset.UtcNow.AddDays(-120) },
                new McpActivityLog { UserId = "u", Tool = "recent", Timestamp = DateTimeOffset.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();

            // Relational branch uses ExecuteDeleteAsync; throws here if it can't translate.
            var count = await McpActivityPurgeService.PurgeOnce(db, retentionDays: 90, CancellationToken.None);

            Assert.Equal(1, count);
            var remaining = await db.McpActivityLogs.Select(a => a.Tool).ToListAsync();
            Assert.Equal(new[] { "recent" }, remaining);
        }
    }
}
