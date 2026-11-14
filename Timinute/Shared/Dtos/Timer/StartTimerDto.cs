using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos.Timer
{
    public class StartTimerDto
    {
        // Same rule as CreateTrackedTaskDto.Name — stopping the timer turns this into a TrackedTask.
        [Required]
        [StringLength(50, ErrorMessage = "Name of task must be between 2 and 50 characters long.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        public string? ProjectId { get; set; }
    }
}
