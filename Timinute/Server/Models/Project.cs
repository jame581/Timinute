namespace Timinute.Server.Models
{
    public class Project : ISoftDeletable
    {
        public string ProjectId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ICollection<TrackedTask>? TrackedTasks { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
