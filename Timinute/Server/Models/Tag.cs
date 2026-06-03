namespace Timinute.Server.Models
{
    public class Tag
    {
        public string TagId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Color { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public ICollection<TrackedTask> TrackedTasks { get; set; } = new List<TrackedTask>();
    }
}
