using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Validators;

namespace Timinute.Shared.Dtos.TrackedTask
{
    public class UpdateTrackedTaskDto
    {
        [Required]
        public string TaskId { get; set; } = null!;

        [Required]
        [StringLength(50, ErrorMessage = "Name of task must be between 2 and 50 characters long.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [Required]
        [NonDefaultDateTimeOffset]
        public DateTimeOffset StartDate { get; set; }

        public DateTimeOffset? EndDate { get; set; }

        public string? ProjectId { get; set; }

        public ProjectDto? Project { get; set; }
    }
}
