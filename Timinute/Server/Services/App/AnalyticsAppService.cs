using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Shared.Dtos.Analytics;

namespace Timinute.Server.Services.App
{
    public interface IAnalyticsAppService
    {
        Task<AnalyticsSummaryDto> SummaryAsync(string userId, DateTimeOffset from, DateTimeOffset to, int tzOffsetMinutes);
    }

    /// <summary>
    /// userId-parameterised analytics summary shared by <c>AnalyticsController</c> and
    /// the Task 7 MCP tools. The user+range filter runs in SQL; grouping and summing are
    /// done IN MEMORY (SUM over TimeSpan does not translate to SQL). Reads
    /// <c>Preferences.WorkdayHoursPerDay</c> off <c>dbContext.Users</c>, so it needs the
    /// DbContext directly per plan R5. Lifted verbatim from
    /// <c>AnalyticsController.GetRangeSummary</c>.
    /// </summary>
    public class AnalyticsAppService : IAnalyticsAppService
    {
        private readonly ApplicationDbContext dbContext;

        public AnalyticsAppService(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<AnalyticsSummaryDto> SummaryAsync(string userId, DateTimeOffset from, DateTimeOffset to, int tzOffsetMinutes)
        {
            var offset = TimeSpan.FromMinutes(tzOffsetMinutes);

            var fromUtc = from.ToUniversalTime();
            var toUtc = to.ToUniversalTime();
            var rows = await dbContext.TrackedTasks
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.StartDate >= fromUtc && t.StartDate <= toUtc)
                .Select(t => new { t.StartDate, t.Duration })
                .ToListAsync();

            var totalTicks = rows.Sum(r => r.Duration.Ticks);
            var activeDays = rows
                .GroupBy(r => r.StartDate.ToOffset(offset).Date)
                .Count();

            var workdayHours = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Preferences.WorkdayHoursPerDay)
                .FirstOrDefaultAsync();
            if (workdayHours <= 0) workdayHours = 8.0m;

            var weekdayCount = CountWeekdays(
                from.ToOffset(offset).Date,
                to.ToOffset(offset).Date);

            return new AnalyticsSummaryDto
            {
                TotalDuration = TimeSpan.FromTicks(totalTicks),
                TaskCount = rows.Count,
                ActiveDays = activeDays,
                AveragePerActiveDay = activeDays == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(totalTicks / activeDays),
                WeekdayCount = weekdayCount,
                TargetDuration = TimeSpan.FromHours((double)(workdayHours * weekdayCount))
            };
        }

        private static int CountWeekdays(DateTime fromDate, DateTime toDate)
        {
            var count = 0;
            for (var day = fromDate; day <= toDate; day = day.AddDays(1))
            {
                if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday) count++;
            }
            return count;
        }
    }
}
