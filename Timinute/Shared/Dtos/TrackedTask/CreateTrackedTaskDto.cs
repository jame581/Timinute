using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Validators;

namespace Timinute.Shared.Dtos.TrackedTask
{
    public class CreateTrackedTaskDto
    {
        [Required]
        [StringLength(50, ErrorMessage = "Name of task is too long.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        [MinDuration]
        public TimeSpan Duration { get; set; }

        public string? ProjectId { get; set; }

        public ProjectDto? Project { get; set; }
    }
}
