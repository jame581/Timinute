using System;

namespace Timinute.Shared.Dtos.Analytics
{
    public class AnalyticsSummaryDto
    {
        public TimeSpan TotalDuration { get; set; }
        public int TaskCount { get; set; }
        public int ActiveDays { get; set; }
        public TimeSpan AveragePerActiveDay { get; set; }
        // Mon–Fri days in the (tz-shifted) range, and WorkdayHoursPerDay × that.
        public int WeekdayCount { get; set; }
        public TimeSpan TargetDuration { get; set; }
    }
}
