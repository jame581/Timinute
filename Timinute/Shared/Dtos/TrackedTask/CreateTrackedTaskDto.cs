using Timinute.Shared.Dtos.Project;

namespace Timinute.Shared.Dtos.TrackedTask
{
    public class CreateTrackedTaskDto
    {
        public string Name { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ProjectId { get; set; }
        public ProjectDto? Project { get; set; }
    }
}
