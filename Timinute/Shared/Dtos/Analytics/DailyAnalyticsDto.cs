using System;

namespace Timinute.Shared.Dtos.Analytics
{
    public class DailyAnalyticsDto
    {
        // Local calendar day (time component is 00:00) after TzOffsetMinutes shift.
        public DateTime Date { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int TaskCount { get; set; }
    }
}
