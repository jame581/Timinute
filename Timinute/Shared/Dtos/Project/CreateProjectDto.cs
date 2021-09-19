namespace Timinute.Shared.Dtos.Project
{
    public class CreateProjectDto
    {
        public string Name { get; set; } = null!;
        public string? CompanyId { get; set; }
    }
}
