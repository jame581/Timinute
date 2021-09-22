using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Models
{
    public class TrackedTask
    {
        public string TaskId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public TimeSpan Duration { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? ProjectId { get; set; }
        public string UserId { get; set; } = null!;

        public TrackedTask()
        {

        }

        public TrackedTask(TrackedTaskDto trackedTask)
        {
            TaskId = trackedTask.TaskId;
            Name = trackedTask.Name;
            Duration = trackedTask.Duration;
            StartDate = trackedTask.StartDate;
            EndDate = trackedTask.EndDate;
            ProjectId = trackedTask.ProjectId;
            UserId = trackedTask.UserId;
        }
    }
}
