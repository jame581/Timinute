using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Services;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    public class McpActivityPurgeServiceTest
    {
        [Fact]
        public async Task PurgeOnce_Deletes_Older_Than_Cutoff_Only()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            db.McpActivityLogs.AddRange(
                new McpActivityLog { UserId = "u", Tool = "old", Timestamp = DateTimeOffset.UtcNow.AddDays(-120) },
                new McpActivityLog { UserId = "u", Tool = "new", Timestamp = DateTimeOffset.UtcNow.AddDays(-10) });
            await db.SaveChangesAsync();

            var count = await McpActivityPurgeService.PurgeOnce(db, retentionDays: 90, CancellationToken.None);

            var remaining = db.McpActivityLogs.Select(a => a.Tool).ToList();
            Assert.Equal(new[] { "new" }, remaining);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task PurgeOnce_Keeps_Rows_Inside_Retention_Boundary()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var cutoffTime = DateTimeOffset.UtcNow.AddDays(-90);
            db.McpActivityLogs.AddRange(
                new McpActivityLog { UserId = "u", Tool = "outside", Timestamp = cutoffTime.AddSeconds(-1) },
                new McpActivityLog { UserId = "u", Tool = "boundary", Timestamp = cutoffTime.AddDays(1) });
            await db.SaveChangesAsync();

            var count = await McpActivityPurgeService.PurgeOnce(db, retentionDays: 90, CancellationToken.None);

            var remaining = db.McpActivityLogs.Select(a => a.Tool).OrderBy(t => t).ToList();
            Assert.Equal(new[] { "boundary" }, remaining);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task PurgeOnce_Negative_Retention_Does_Not_Purge_Recent_Rows()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            // A misconfigured negative retention must never move the cutoff into the future
            // and wipe recent rows; it is clamped to a >=1-day window.
            db.McpActivityLogs.AddRange(
                new McpActivityLog { UserId = "u", Tool = "today", Timestamp = DateTimeOffset.UtcNow },
                new McpActivityLog { UserId = "u", Tool = "ancient", Timestamp = DateTimeOffset.UtcNow.AddDays(-200) });
            await db.SaveChangesAsync();

            var count = await McpActivityPurgeService.PurgeOnce(db, retentionDays: -5, CancellationToken.None);

            var remaining = db.McpActivityLogs.Select(a => a.Tool).ToList();
            Assert.Contains("today", remaining);
            Assert.DoesNotContain("ancient", remaining);
            Assert.Equal(1, count);
        }
    }
}
