using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Shared.Dtos.TrackedTask
{
    public class UpdateTrackedTaskDto
    {
        [Required]
        public string TaskId { get; set; } = null!;

        [Required]
        [StringLength(50, ErrorMessage = "Name of task is too long.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? ProjectId { get; set; }

        public ProjectDto? Project { get; set; }
    }
}
