using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Models;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    // RunningTimer's PK is UserId: the database itself enforces "one running timer per user".
    // SQLite (not InMemory) so the key and the cascade are real relational constraints.
    public class RunningTimerSqliteTest
    {
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        [Fact]
        public async Task Second_Row_For_Same_User_Violates_Key()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var db = await TestHelper.GetSqliteApplicationDbContext(connection);

            db.RunningTimers.Add(new RunningTimer { UserId = SeedUserId1, Name = "First", StartDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            db.RunningTimers.Add(new RunningTimer { UserId = SeedUserId1, Name = "Second", StartDate = DateTimeOffset.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task Deleting_User_Cascades_RunningTimer()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var db = await TestHelper.GetSqliteApplicationDbContext(connection);

            var user = new ApplicationUser { Id = "cascade-user", UserName = "cascade@example.com", Email = "cascade@example.com", FirstName = "Cas", LastName = "Cade" };
            db.Users.Add(user);
            db.RunningTimers.Add(new RunningTimer { UserId = user.Id, Name = "Running", StartDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            db.Users.Remove(user);
            await db.SaveChangesAsync();

            Assert.False(await db.RunningTimers.AnyAsync(t => t.UserId == user.Id));
        }
    }
}
