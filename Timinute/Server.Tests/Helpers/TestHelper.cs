using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Helpers;
using System;

namespace Timinute.Server.Tests.Helpers
{
    public static class TestHelper
    {
        public static DbContextOptions<ApplicationDbContext> GetDbContextOptions(string dbName = "Test_DB")
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
              .UseInMemoryDatabase(databaseName: dbName)
              .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
              .EnableSensitiveDataLogging()
              .EnableDetailedErrors()
              .Options;
        }

        public static async Task<ApplicationDbContext> GetDefaultApplicationDbContext(string databaseName = "Test_DB", bool fillTestDate = true, bool deleteAll = true)
        {
            var someOptions = Options.Create(new OperationalStoreOptions());
           
            var dbContextOptions = GetDbContextOptions(databaseName);

            var context = new ApplicationDbContext(dbContextOptions, someOptions);

            if (deleteAll)
            {
                await context.Database.EnsureDeletedAsync();
            }

            if (fillTestDate)
            {
                await FillInitData(context);
            }
            return context;
        }


        private static async Task FillInitData(ApplicationDbContext context)
        {
            var roles = new List<ApplicationRole>
            {
                new ApplicationRole{Name = Constants.Roles.Basic, NormalizedName =  Constants.Roles.Basic.ToUpper(), Description = "Basic role with lowest rights."},
                new ApplicationRole{Name = Constants.Roles.Admin, NormalizedName =  Constants.Roles.Admin.ToUpper(), Description = "Admin role with highest rights."}
            };

            var applicationUsers = new List<ApplicationUser>()
            {
                new ApplicationUser { Id = "ApplicationUser1", Email = "test1@email.com", FirstName = "Jan", LastName = "Testovic", EmailConfirmed = true, UserName = "janek", PasswordHash = "3037c1616052562ebec4291009d17541"},
                new ApplicationUser { Id = "ApplicationUser2", Email = "test2@email.com", FirstName = "Ivana", LastName = "Maricenkova", EmailConfirmed = true, UserName = "marika", PasswordHash = "3037c1616052562ebec4291009d17541"},
                new ApplicationUser { Id = "ApplicationUser3", Email = "test3@email.com", FirstName = "Marek", LastName = "Klukac", EmailConfirmed = true, UserName = "klukac", PasswordHash = "3037c1616052562ebec4291009d17541"},
            };

            var trackedTasks = new List<TrackedTask>
            {
                new TrackedTask { TaskId = "TrackedTaskId1", Name = "Task 1", User = applicationUsers[0], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 10, 0, 0), Duration = TimeSpan.FromHours(2) },
                new TrackedTask { TaskId = "TrackedTaskId2", Name = "Task 2", User = applicationUsers[0], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 12, 0, 0), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "TrackedTaskId3", Name = "Task 3", User = applicationUsers[0], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 12, 0, 0), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "TrackedTaskId4", Name = "Task 4", User = applicationUsers[1], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 12, 0, 0), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "TrackedTaskId5", Name = "Task 5", User = applicationUsers[1], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 14, 0, 0), Duration = TimeSpan.FromHours(6) },
                new TrackedTask { TaskId = "TrackedTaskId6", Name = "Task 6", User = applicationUsers[1], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 14, 0, 0), Duration = TimeSpan.FromHours(6) },
                new TrackedTask { TaskId = "TrackedTaskId7", Name = "Task 7", User = applicationUsers[2], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 15, 0, 0), Duration = TimeSpan.FromHours(7) },
            };

            var projects = new List<Project>
            {
                new Project { ProjectId = "ProjectId1", Name = "Project 1", CompanyId = "CompanyId1", TrackedTasks = new List<TrackedTask>() { trackedTasks[0], trackedTasks[1] } },
                new Project { ProjectId = "ProjectId2", Name = "Project 2", CompanyId = "CompanyId1", TrackedTasks = new List<TrackedTask>() { trackedTasks[2], trackedTasks[3] } },
                new Project { ProjectId = "ProjectId3", Name = "Project 3", CompanyId = "CompanyId1", TrackedTasks = new List<TrackedTask>() { trackedTasks[4], trackedTasks[5] } },
            };

            var companies = new List<Company>
            {
                new Company { CompanyId = "CompanyId1", Name = "Company 1", Projects = projects },
            };

            context.Roles.AddRange(roles);
            context.Users.AddRange(applicationUsers);
            context.TrackedTasks.AddRange(trackedTasks);
            context.Projects.AddRange(projects);
            context.Companies.AddRange(companies);

            await context.SaveChangesAsync();
        }
    }
}
