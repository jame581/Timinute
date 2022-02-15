using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;

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

        public static async Task<ApplicationDbContext> GetDefaultApplicationDbContext(string databaseName = "Test_DB", bool fillTestDate = true, bool deleteAll = true, bool analyticsTest = false)
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

            if (analyticsTest)
            {
                await FillAnalyticsData(context);
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
                new Project { ProjectId = "ProjectId1", Name = "Project 1", TrackedTasks = new List<TrackedTask>() { trackedTasks[0], trackedTasks[1] } },
                new Project { ProjectId = "ProjectId2", Name = "Project 2", TrackedTasks = new List<TrackedTask>() { trackedTasks[2], trackedTasks[3] } },
                new Project { ProjectId = "ProjectId3", Name = "Project 3", TrackedTasks = new List<TrackedTask>() { trackedTasks[4], trackedTasks[5] } },
            };

            context.Roles.AddRange(roles);
            context.Users.AddRange(applicationUsers);
            context.TrackedTasks.AddRange(trackedTasks);
            context.Projects.AddRange(projects);

            await context.SaveChangesAsync();
        }

        private static async Task FillAnalyticsData(ApplicationDbContext context)
        {

            var applicationUsers = new List<ApplicationUser>()
            {
                new ApplicationUser { Id = "ApplicationUser1000", Email = "test1000@email.com", FirstName = "Jan", LastName = "Testovic", EmailConfirmed = true, UserName = "janek100", PasswordHash = "3037c1616052562ebec4291009d17541"},
            };

            var today = DateTime.Today;
            var month = new DateTime(today.Year, today.Month, 1);
            var first = month.AddMonths(-1);

            var trackedTasks = new List<TrackedTask>
            {
                new TrackedTask { TaskId = "TrackedTaskId1001", Name = "Task 1001", ProjectId = "ProjectId1001", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(1), Duration = TimeSpan.FromHours(1) },
                new TrackedTask { TaskId = "TrackedTaskId1002", Name = "Task 1002", ProjectId = "ProjectId1001", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(2), Duration = TimeSpan.FromHours(2) },
                new TrackedTask { TaskId = "TrackedTaskId1003", Name = "Task 1003", ProjectId = "ProjectId1002", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(3), Duration = TimeSpan.FromHours(3) },
                new TrackedTask { TaskId = "TrackedTaskId1004", Name = "Task 1004", ProjectId = "ProjectId1002", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(4), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "TrackedTaskId1005", Name = "Task 1005", ProjectId = "ProjectId1003", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(5), Duration = TimeSpan.FromHours(5) },
                new TrackedTask { TaskId = "TrackedTaskId1006", Name = "Task 1006", ProjectId = "ProjectId1003", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(6), Duration = TimeSpan.FromHours(6) },
                
                new TrackedTask { TaskId = "TrackedTaskId1007", Name = "Task 1007", User = applicationUsers[0], StartDate = first, EndDate = first.AddHours(7), Duration = TimeSpan.FromHours(7) },

                new TrackedTask { TaskId = "TrackedTaskId1008", Name = "Task 1008", User = applicationUsers[0], StartDate = month, EndDate = month.AddHours(1), Duration = TimeSpan.FromHours(1) },
                new TrackedTask { TaskId = "TrackedTaskId1009", Name = "Task 1009", User = applicationUsers[0], StartDate = month, EndDate = month.AddHours(2), Duration = TimeSpan.FromHours(2) },
                new TrackedTask { TaskId = "TrackedTaskId1010", Name = "Task 1010", User = applicationUsers[0], StartDate = month, EndDate = month.AddHours(3), Duration = TimeSpan.FromHours(3) },
                new TrackedTask { TaskId = "TrackedTaskId1011", Name = "Task 1011", User = applicationUsers[0], StartDate = month, EndDate = month.AddHours(4), Duration = TimeSpan.FromHours(4) },
            };

            var projects = new List<Project>
            {
                new Project { ProjectId = "ProjectId1001", Name = "Project 1001", TrackedTasks = new List<TrackedTask>() { trackedTasks[0], trackedTasks[1] } },
                new Project { ProjectId = "ProjectId1002", Name = "Project 1002", TrackedTasks = new List<TrackedTask>() { trackedTasks[2], trackedTasks[3] } },
                new Project { ProjectId = "ProjectId1003", Name = "Project 1003", TrackedTasks = new List<TrackedTask>() { trackedTasks[4], trackedTasks[5] } },
                new Project { ProjectId = "ProjectId1004", Name = "Project 1004", TrackedTasks = new List<TrackedTask>() { trackedTasks[7], trackedTasks[8], trackedTasks[9], trackedTasks[10], } },
            };

            context.Users.AddRange(applicationUsers);
            context.TrackedTasks.AddRange(trackedTasks);
            context.Projects.AddRange(projects);

            await context.SaveChangesAsync();
        }
    }
}
