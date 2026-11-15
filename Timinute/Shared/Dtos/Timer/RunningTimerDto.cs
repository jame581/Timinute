namespace Timinute.Shared.Dtos.Timer
{
    public class RunningTimerDto
    {
        public string Name { get; set; } = null!;
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectColor { get; set; }
        public DateTimeOffset StartDate { get; set; }
    }
}
