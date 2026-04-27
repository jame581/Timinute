namespace Timinute.Server.Models.Export
{
    public class AnalyticsExportDto
    {
        public string Month { get; set; } = null!;
        public string TotalHours { get; set; } = null!;
        public string TopProject { get; set; } = null!;
        public string TopProjectHours { get; set; } = null!;
    }
}
