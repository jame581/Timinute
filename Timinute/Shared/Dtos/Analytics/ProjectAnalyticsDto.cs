using System;

namespace Timinute.Shared.Dtos.Analytics
{
    public class ProjectAnalyticsDto
    {
        // "_none" for tasks without a project.
        public string ProjectId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Color { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int TaskCount { get; set; }
    }
}
