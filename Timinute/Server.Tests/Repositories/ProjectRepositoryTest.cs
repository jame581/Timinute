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
                item => Assert.Contains("ProjectId3", item.ProjectId));
        }

        [Fact]
        public async void Get_Project_By_ProjectId_Test()
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
        public async void Get_Project_Where_ProjectId_Test()
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
        public async void Get_Project_By_Name_Test()
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
        public async void Get_Project_By_CompanyId_Test()
        {
            using (var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName))
            {
                // Call the function to test
                var repository = new BaseRepository<Project>(dbContext);
                var projects = await repository.Get(x => x.CompanyId == "CompanyId1");

                Assert.NotNull(projects);

                Assert.Collection(projects,
                    item => Assert.Contains("ProjectId1", item.ProjectId),
                    item => Assert.Contains("ProjectId2", item.ProjectId),
                    item => Assert.Contains("ProjectId3", item.ProjectId));
            }
        }

        [Fact]
        public async Task Add_Project_Test()
        {
            var newProject = new Project { ProjectId = "ProjectId100", Name = "Project 100", CompanyId = "CompanyId1" };

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
            var newProject = new Project { ProjectId = "ProjectId200", Name = "Project 200" };

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
                Assert.Equal("Project 1", project.Name);
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
                Assert.Equal("newName", changedProject.Name);
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
    }
}
