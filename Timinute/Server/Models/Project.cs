namespace Timinute.Server.Models
{
    public class Project
    {
        public string ProjectId {  get; set; } = null!;
        public string Name {  get; set; } = null!;
        public string? CompanyId {  get; set; }
        public Company? Company {  get; set; }
        public ICollection<TrackedTask>? TrackedTasks {  get; set; }
    }
}
