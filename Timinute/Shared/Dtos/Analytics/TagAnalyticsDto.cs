using System;

namespace Timinute.Shared.Dtos.Analytics
{
    public class TagAnalyticsDto
    {
        // "_untagged" for tasks without any tag.
        public string TagId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Color { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int TaskCount { get; set; }
    }
}
