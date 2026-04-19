using Timinute.Server.Models;
using Timinute.Server.Repository;

namespace Timinute.Server.Services
{
    public class TrashPurgeService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IConfiguration configuration;
        private readonly ILogger<TrashPurgeService> logger;

        public TrashPurgeService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TrashPurgeService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.configuration = configuration;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = configuration.GetValue<int>("TrashRetention:PurgeIntervalHours", 24);
            var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

            await RunOnce(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnce(stoppingToken);
            }
        }

        public async Task RunOnce(CancellationToken cancellationToken)
        {
            try
            {
                var retentionDays = configuration.GetValue<int>("TrashRetention:Days", 30);
                var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

                using var scope = scopeFactory.CreateScope();
                var repoFactory = scope.ServiceProvider.GetRequiredService<IRepositoryFactory>();

                var taskRepo = repoFactory.GetRepository<TrackedTask>();
                var projectRepo = repoFactory.GetRepository<Project>();

                var taskCount = await taskRepo.PurgeExpired(cutoff);
                var projectCount = await projectRepo.PurgeExpired(cutoff);

                logger.LogInformation("TrashPurge: purged {taskCount} tasks and {projectCount} projects older than {cutoff}",
                    taskCount, projectCount, cutoff);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TrashPurge tick failed");
            }
        }
    }
}
