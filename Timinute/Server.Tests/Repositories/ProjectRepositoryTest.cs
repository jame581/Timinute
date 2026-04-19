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
    public class ProjectRepositoryTest
    {
        private const string dbName = "Project_DB";

        [Fact]
        public async Task GetAllProjects_Returns_Projects()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);

            //Execute method of SUT
            var repository = new BaseRepository<Project>(dbContext);
            var projects = await repository.Get();

            //Assert  
            Assert.NotNull(projects);
            Assert.IsAssignableFrom<List<Project>>(projects);

            Assert.Collection(projects,
                item => Assert.Contains("ProjectId1", item.ProjectId),
                item => Assert.Contains("ProjectId2", item.ProjectId),
                item => Assert.Contains("ProjectId3", item.ProjectId),
                item => Assert.Contains("ProjectId4", item.ProjectId),
                item => Assert.Contains("ProjectId5", item.ProjectId));
        }

        [Fact]
        public async Task Get_Project_By_ProjectId_Test()
        {
            await using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);
                var result = await repository.Get(x => x.ProjectId == "ProjectId1");

                // Verify the results
                Assert.NotNull(result);
                Assert.Collection(result, item => Assert.Contains("ProjectId1", item.ProjectId));
            }
        }

        [Fact]
        public async Task Get_Project_Where_ProjectId_Test()
        {
            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);
                var projects = await repository.Get(x => x.ProjectId == "ProjectId2");

                Assert.NotNull(projects);
                Assert.Single(projects);
                Assert.Collection(projects, item => Assert.Contains("ProjectId2", item.ProjectId));
            }
        }

        [Fact]
        public async Task Get_Project_By_Name_Test()
        {
            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);
                var projects = await repository.Get(x => x.Name == "Project 1");

                Assert.NotNull(projects);
                Assert.Single(projects);
                Assert.Collection(projects, item => Assert.Contains("ProjectId1", item.ProjectId));
            }
        }

        [Fact]
        public async Task Add_Project_Test()
        {
            var userId = Guid.NewGuid().ToString();
            var newProject = new Project { ProjectId = "ProjectId100", Name = "Project 100", UserId = userId };

            int countBefore;

            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                countBefore = dbContext.Projects.Count();
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);
                await repository.Insert(newProject);
            }

            // Verify the results
            // Use a clean instance of the context to run the test
            using var cleanContextInstance = await TestHelper.GetDefaultApplicationDbContext(dbName, false, false);
            {
                Assert.Equal(countBefore + 1, cleanContextInstance.Projects.Count());
                Assert.True(cleanContextInstance.Projects.Contains(newProject));
            }
        }

        [Fact]
        public async Task Add_Project_Without_Company_Test()
        {
            var userId = Guid.NewGuid().ToString();
            var newProject = new Project { ProjectId = "ProjectId200", Name = "Project 200", UserId = userId };

            int countBefore;

            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                countBefore = dbContext.Projects.Count();
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);
                await repository.Insert(newProject);
            }

            // Verify the results
            // Use a clean instance of the context to run the test
            using var cleanContextInstance = await TestHelper.GetDefaultApplicationDbContext(dbName, false, false);
            {
                Assert.Equal(countBefore + 1, cleanContextInstance.Projects.Count());
                Assert.True(cleanContextInstance.Projects.Contains(newProject));
            }
        }


        [Fact]
        public async Task Update_Project_Test()
        {
            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                var repository = new BaseRepository<Project>(dbContext);

                // Check beofre
                var project = await repository.GetById("ProjectId1");
                Assert.NotNull(project);
                Assert.Equal("Project 1", project!.Name);
                project.Name = "newName";
                await repository.Update(project);
            }

            // Verify the results
            // Use a clean instance of the context to run the test
            using (var cleanContextInstance = await TestHelper.GetDefaultApplicationDbContext(dbName, false, false))
            {
                // Check beofre
                var changedProject = cleanContextInstance.Projects.FirstOrDefault(x => x.ProjectId == "ProjectId1");
                Assert.NotNull(changedProject);
                Assert.Equal("newName", changedProject!.Name);
            }
        }

        [Fact]
        public async Task Update_Non_Existing_Project_Test()
        {
            var trackedTaskToUpdate = new Project { ProjectId = "aaaa", Name = "NewName" };
            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);

                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await repository.Update(trackedTaskToUpdate));
            }
        }

        [Fact]
        public async Task Delete_Existing_Project_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);
            var repository = new BaseRepository<Project>(dbContext);

            var project = await repository.GetById("ProjectId4");
            Assert.NotNull(project);

            await repository.Delete(project!);

            var deleted = await repository.GetById("ProjectId4");
            Assert.Null(deleted);
        }

        [Fact]
        public async Task Delete_Project_By_Id_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName);
            var repository = new BaseRepository<Project>(dbContext);

            await repository.Delete("ProjectId5");

            var deleted = await repository.GetById("ProjectId5");
            Assert.Null(deleted);
        }

        [Fact]
        public async Task SoftDelete_Marks_Entity_And_Hides_From_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "SoftDelete");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId4");

            var found = await repository.GetById("ProjectId4");
            Assert.Null(found);

            var stillInDb = await dbContext.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == "ProjectId4");
            Assert.NotNull(stillInDb);
            Assert.NotNull(stillInDb!.DeletedAt);
        }

        [Fact]
        public async Task Restore_Clears_DeletedAt_And_Restores_To_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Restore");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId5");
            Assert.Null(await repository.GetById("ProjectId5"));

            await repository.Restore("ProjectId5");

            var restored = await repository.GetById("ProjectId5");
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
        }

        [Fact]
        public async Task GetDeleted_Returns_Only_SoftDeleted_Entities_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "GetDeleted");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId4");
            await repository.SoftDelete("ProjectId5");

            var deleted = (await repository.GetDeleted()).ToList();

            Assert.Equal(2, deleted.Count);
            Assert.All(deleted, p => Assert.NotNull(p.DeletedAt));
            Assert.Contains(deleted, p => p.ProjectId == "ProjectId4");
            Assert.Contains(deleted, p => p.ProjectId == "ProjectId5");
        }

        [Fact]
        public async Task PurgeExpired_Removes_Old_SoftDeleted_Only_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Purge");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId4");
            await repository.SoftDelete("ProjectId5");

            var aged = await dbContext.Projects.IgnoreQueryFilters().AsTracking()
                .FirstAsync(p => p.ProjectId == "ProjectId4");
            aged.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await dbContext.SaveChangesAsync();

            var purgedCount = await repository.PurgeExpired(DateTimeOffset.UtcNow.AddDays(-30));

            Assert.Equal(1, purgedCount);

            var remaining = await dbContext.Projects.IgnoreQueryFilters().Where(p => p.ProjectId == "ProjectId4" || p.ProjectId == "ProjectId5").ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("ProjectId5", remaining[0].ProjectId);
        }
    }
}
