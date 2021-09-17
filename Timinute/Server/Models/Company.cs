namespace Timinute.Server.Models
{
    public class Company
    {
        public string CompanyId {  get; set; } = null!;

        public string Name { get; set; } = null!;

        public ICollection<Project>? Projects {  get; set; }
    }
}
