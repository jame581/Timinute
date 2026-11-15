namespace Timinute.Shared.Dtos.Timer
{
    public class StopTimerDto
    {
        /// <summary>Optional end time (idle trim). Defaults to the server's current time.</summary>
        public DateTimeOffset? EndAt { get; set; }
    }
}
