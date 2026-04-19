using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Services;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    public class TrashPurgeServiceTest
    {
        [Fact]
        public async Task RunOnce_Purges_Expired_Tasks_And_Projects_Only_Test()
        {
            var dbContext = await TestHelper.GetDefaultApplicationDbContext("TrashPurgeService_DB");
            var services = new ServiceCollection();
            services.AddSingleton(dbContext);
            services.AddTransient<IRepositoryFactory>(sp => new RepositoryFactory(sp.GetRequiredService<ApplicationDbContext>()));
            var provider = services.BuildServiceProvider();

            var repoFactory = provider.GetRequiredService<IRepositoryFactory>();
            var taskRepo = repoFactory.GetRepository<TrackedTask>();
            var projRepo = repoFactory.GetRepository<Project>();

            await taskRepo.SoftDelete("TrackedTaskId1");
            await taskRepo.SoftDelete("TrackedTaskId2");
            await projRepo.SoftDelete("ProjectId4");
            await projRepo.SoftDelete("ProjectId5");

            var oldTask = await dbContext.TrackedTasks.IgnoreQueryFilters().AsTracking()
                .FirstAsync(t => t.TaskId == "TrackedTaskId1");
            oldTask.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            var oldProject = await dbContext.Projects.IgnoreQueryFilters().AsTracking()
                .FirstAsync(p => p.ProjectId == "ProjectId4");
            oldProject.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await dbContext.SaveChangesAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["TrashRetention:Days"] = "30",
                    ["TrashRetention:PurgeIntervalHours"] = "24"
                })
                .Build();

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(s => s.ServiceProvider).Returns(provider);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var logger = new Mock<ILogger<TrashPurgeService>>();
            var service = new TrashPurgeService(scopeFactoryMock.Object, configuration, logger.Object);

            await service.RunOnce(CancellationToken.None);

            Assert.Null(await dbContext.TrackedTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1"));
            Assert.NotNull(await dbContext.TrackedTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId2"));
            Assert.Null(await dbContext.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == "ProjectId4"));
            Assert.NotNull(await dbContext.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == "ProjectId5"));
        }
    }
}
