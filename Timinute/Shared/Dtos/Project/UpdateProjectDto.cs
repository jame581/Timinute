namespace Timinute.Shared.Dtos.Project
{
    public class UpdateProjectDto
    {
        public string ProjectId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? CompanyId { get; set; }
        public CompanyDto? Company { get; set; }
    }
}
