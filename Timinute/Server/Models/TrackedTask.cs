namespace Timinute.Server.Models
{
    public class TrackedTask : ISoftDeletable
    {
        public string TaskId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public TimeSpan Duration { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string? ProjectId { get; set; }
        public Project? Project { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
