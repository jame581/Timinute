using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    public class TrackedTaskRepositoryTest
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        private const string dbName = "TrackedTask_DB";

        [Fact]
        public async Task GetAllTrackedTasks_Returns_TrackedTasks()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            //Execute method of SUT
            var repository = new BaseRepository<TrackedTask>(dbContext);
            var trackedTasks = await repository.Get();

            //Assert  
            Assert.NotNull(trackedTasks);
            Assert.IsAssignableFrom<List<TrackedTask>>(trackedTasks);

            Assert.Collection(trackedTasks,
                item => Assert.Contains("TrackedTaskId1", item.TaskId),
                item => Assert.Contains("TrackedTaskId2", item.TaskId),
                item => Assert.Contains("TrackedTaskId3", item.TaskId),
                item => Assert.Contains("TrackedTaskId4", item.TaskId),
                item => Assert.Contains("TrackedTaskId5", item.TaskId),
                item => Assert.Contains("TrackedTaskId6", item.TaskId),
                item => Assert.Contains("TrackedTaskId7", item.TaskId),
                item => Assert.Contains("TrackedTaskId8", item.TaskId));
        }

        [Fact]
        public async Task Get_TrackedTask_By_TaskId_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            // Call the function to test
            var repository = new BaseRepository<TrackedTask>(dbContext);
            var result = await repository.Get(x => x.TaskId == "TrackedTaskId1");

            // Verify the results
            Assert.NotNull(result);
            Assert.Collection(result, item => Assert.Contains("TrackedTaskId1", item.TaskId));
        }

        [Fact]
        public async Task Get_TrackedTask_Where_TaskId_Test()
        {
            using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            // Call the function to test
            var repository = new BaseRepository<TrackedTask>(dbContext);
            var trackedTasks = await repository.Get(x => x.TaskId == "TrackedTaskId2");

            Assert.NotNull(trackedTasks);
            Assert.Single(trackedTasks);
            Assert.Collection(trackedTasks, item => Assert.Contains("TrackedTaskId2", item.TaskId));
        }

        [Fact]
        public async Task Get_TrackedTask_By_Name_Test()
        {
            using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            // Call the function to test
            var repository = new BaseRepository<TrackedTask>(dbContext);
            var trackedTasks = await repository.Get(x => x.Name == "Task 1");

            Assert.NotNull(trackedTasks);
            Assert.Single(trackedTasks);
            Assert.Collection(trackedTasks, item => Assert.Contains("TrackedTaskId1", item.TaskId));
        }

        [Fact]
        public async Task Get_TrackedTask_By_Duration_Test()
        {
            using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            // Call the function to test
            var repository = new BaseRepository<TrackedTask>(dbContext);
            var trackedTasks = await repository.Get(x => x.Duration == TimeSpan.FromHours(2));

            Assert.NotNull(trackedTasks);
            Assert.Single(trackedTasks);
            Assert.Collection(trackedTasks, item => Assert.Contains("TrackedTaskId1", item.TaskId));
        }

        [Fact]
        public async Task Add_TrackedTask_Test()
        {
            var dateNow = DateTimeOffset.UtcNow;
            var newTrackedTask = new TrackedTask { TaskId = "TrackedTaskId100", Name = "Task 100", UserId = "ApplicationUser1", StartDate = dateNow, EndDate = dateNow.AddHours(3), Duration = TimeSpan.FromHours(3) };

            int countBefore;

            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                countBefore = dbContext.TrackedTasks.Count();
                // Call the function to test
                var repository = new BaseRepository<TrackedTask>(dbContext);
                await repository.Insert(newTrackedTask);
            }

            // Verify the results
            // Use a clean instance of the context to run the test
            using var cleanContextInstance = await TestHelper.GetDefaultApplicationDbContext(dbName, false, false);
            {
                Assert.Equal(countBefore + 1, cleanContextInstance.TrackedTasks.Count());
                Assert.True(cleanContextInstance.TrackedTasks.Contains(newTrackedTask));
            }
        }

        [Fact]
        public async Task Add_TrackedTask_Without_User_Test()
        {
            var dateNow = DateTimeOffset.UtcNow;
            var newTrackedTask = new TrackedTask { TaskId = "TrackedTaskId100", Name = "Task 100", StartDate = dateNow, EndDate = dateNow.AddHours(3), Duration = TimeSpan.FromHours(3) };

            using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            // Call the function to test
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await Assert.ThrowsAsync<DbUpdateException>(async () => await repository.Insert(newTrackedTask));
        }


        [Fact]
        public async Task Update_TrackedTask_Test()
        {
            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                var repository = new BaseRepository<TrackedTask>(dbContext);

                // Check before
                var trackedTask = await repository.GetById("TrackedTaskId1");
                Assert.NotNull(trackedTask);
                Assert.Equal("Task 1", trackedTask.Name);

                trackedTask.Name = "newName";
                await repository.Update(trackedTask);
            }

            // Verify the results
            // Use a clean instance of the context to run the test
            using var cleanContextInstance = await TestHelper.GetDefaultApplicationDbContext(dbName, false, false);

            // Check beofre
            TrackedTask? changedTrackedTask = cleanContextInstance.TrackedTasks.FirstOrDefault(x => x.TaskId == "TrackedTaskId1");
            Assert.NotNull(changedTrackedTask);
            Assert.Equal("newName", changedTrackedTask!.Name);
        }

        [Fact]
        public async Task Update_Non_Existing_TrackedTask_Test()
        {
            var trackedTaskToUpdate = new TrackedTask { TaskId = "aaaa", Name = "NewName" };
            using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            // Call the function to test
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await repository.Update(trackedTaskToUpdate));
        }

        [Fact]
        public async Task Delete_Existing_TrackedTask_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);
            var repository = new BaseRepository<TrackedTask>(dbContext);

            var task = await repository.GetById("TrackedTaskId1");
            Assert.NotNull(task);

            await repository.Delete(task!);

            var deleted = await repository.GetById("TrackedTaskId1");
            Assert.Null(deleted);
        }

        [Fact]
        public async Task Delete_TrackedTask_By_Id_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.Delete("TrackedTaskId2");

            var deleted = await repository.GetById("TrackedTaskId2");
            Assert.Null(deleted);
        }

        [Fact]
        public async Task SoftDelete_Marks_Entity_And_Hides_From_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "SoftDelete");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId3");

            var found = await repository.GetById("TrackedTaskId3");
            Assert.Null(found);

            var stillInDb = await dbContext.TrackedTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId3");
            Assert.NotNull(stillInDb);
            Assert.NotNull(stillInDb!.DeletedAt);
        }

        [Fact]
        public async Task Restore_Clears_DeletedAt_And_Restores_To_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Restore");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId4");
            Assert.Null(await repository.GetById("TrackedTaskId4"));

            await repository.Restore("TrackedTaskId4");

            var restored = await repository.GetById("TrackedTaskId4");
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
        }

        [Fact]
        public async Task GetDeleted_Returns_Only_SoftDeleted_Entities_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "GetDeleted");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId3");
            await repository.SoftDelete("TrackedTaskId4");

            var deleted = (await repository.GetDeleted()).ToList();

            Assert.Equal(2, deleted.Count);
            Assert.All(deleted, t => Assert.NotNull(t.DeletedAt));
        }

        [Fact]
        public async Task PurgeExpired_Removes_Old_SoftDeleted_Only_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Purge");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId3");
            await repository.SoftDelete("TrackedTaskId4");

            var aged = await dbContext.TrackedTasks.IgnoreQueryFilters().AsTracking()
                .FirstAsync(t => t.TaskId == "TrackedTaskId3");
            aged.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await dbContext.SaveChangesAsync();

            var purgedCount = await repository.PurgeExpired(DateTimeOffset.UtcNow.AddDays(-30));

            Assert.Equal(1, purgedCount);
        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
}
