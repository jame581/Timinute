using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Shared.Dtos.Timer
{
    public class StopTimerResultDto
    {
        public TrackedTaskDto TrackedTask { get; set; } = null!;

        /// <summary>True when the timer's project was deleted mid-run and the entry was saved without one.</summary>
        public bool ProjectCleared { get; set; }
    }
}
