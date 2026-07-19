using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;

namespace Timinute.Server.Services
{
    public class McpActivityPurgeService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IConfiguration configuration;
        private readonly ILogger<McpActivityPurgeService> logger;

        public McpActivityPurgeService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<McpActivityPurgeService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.configuration = configuration;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // PeriodicTimer requires a strictly positive period — clamp to ≥ 1 hour
            // so a misconfigured 0/negative Mcp:ActivityRetention:PurgeIntervalHours doesn't
            // crash the hosted service on startup.
            var intervalHours = Math.Max(1, configuration.GetValue<int>("Mcp:ActivityRetention:PurgeIntervalHours", 24));
            using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

            await RunOnce(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnce(stoppingToken);
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            try
            {
                var retentionDays = configuration.GetValue<int>("Mcp:ActivityRetention:Days", 90);
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var count = await PurgeOnce(db, retentionDays, ct);
                logger.LogInformation("McpActivityPurge: purged {Count} activity rows older than {Days}d", count, retentionDays);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "McpActivityPurge tick failed");
            }
        }

        public static async Task<int> PurgeOnce(ApplicationDbContext db, int retentionDays, CancellationToken ct)
        {
            // A retention window must be at least 1 day. Clamping guards against a misconfigured
            // (negative or zero) value moving the cutoff to now/the future, which would purge every row.
            var days = Math.Max(1, retentionDays);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

            // Relational providers delete set-based in a single round trip without materializing rows.
            // The InMemory provider (unit tests) doesn't support ExecuteDeleteAsync, so fall back to load+remove.
            if (db.Database.IsRelational())
            {
                return await db.McpActivityLogs.Where(a => a.Timestamp < cutoff).ExecuteDeleteAsync(ct);
            }

            var old = await db.McpActivityLogs.Where(a => a.Timestamp < cutoff).ToListAsync(ct);
            db.McpActivityLogs.RemoveRange(old);
            await db.SaveChangesAsync(ct);
            return old.Count;
        }
    }
}
