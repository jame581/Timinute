using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Models
{
    public class TrackedTask
    {
        public string TaskId { get; set; } = null!;

        [Required]
        [StringLength(50, ErrorMessage = "Name is too long.")]
        public string Name { get; set; } = null!;
        
        [Required]
        public TimeSpan Duration { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }
        
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
            StartDate = trackedTask.StartDate.ToLocalTime();
            EndDate = trackedTask.EndDate?.ToLocalTime();
            ProjectId = trackedTask.ProjectId;
            UserId = trackedTask.UserId;
        }
    }
}
