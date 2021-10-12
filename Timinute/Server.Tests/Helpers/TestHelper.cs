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
              .UseInMemoryDatabase(databaseName: dbName).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).EnableSensitiveDataLogging()
              .Options;
        }

        public static async Task<ApplicationDbContext> GetDefaultApplicationDbContext(string databaseName = "Test_DB", bool fillTestDate = true, bool deleteAll = true)
        {
            var someOptions = Options.Create(new OperationalStoreOptions());
            var context = new ApplicationDbContext(GetDbContextOptions(databaseName), someOptions);

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
                new TrackedTask { TaskId = "AppCategoryId1", Name = "Task 1", User = applicationUsers[0], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 10, 0, 0), Duration = TimeSpan.FromHours(2) },
                new TrackedTask { TaskId = "AppCategoryId2", Name = "Task 2", User = applicationUsers[0], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 12, 0, 0), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "AppCategoryId3", Name = "Task 3", User = applicationUsers[0], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 12, 0, 0), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "AppCategoryId4", Name = "Task 4", User = applicationUsers[1], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 12, 0, 0), Duration = TimeSpan.FromHours(4) },
                new TrackedTask { TaskId = "AppCategoryId5", Name = "Task 5", User = applicationUsers[1], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 14, 0, 0), Duration = TimeSpan.FromHours(6) },
                new TrackedTask { TaskId = "AppCategoryId6", Name = "Task 6", User = applicationUsers[1], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 14, 0, 0), Duration = TimeSpan.FromHours(6) },
                new TrackedTask { TaskId = "AppCategoryId7", Name = "Task 7", User = applicationUsers[2], StartDate = new DateTime(2021, 10, 1, 8, 0, 0), EndDate = new DateTime(2021, 10, 1, 15, 0, 0), Duration = TimeSpan.FromHours(7) },
            };

            context.Roles.AddRange(roles);
            context.Users.AddRange(applicationUsers);
            context.TrackedTasks.AddRange(trackedTasks);

            await context.SaveChangesAsync();
        }
    }
}
