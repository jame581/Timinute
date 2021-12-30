using Timinute.Shared.Dtos.Project;

namespace Timinute.Shared.Dtos
{
    public class CompanyDto
    {
        public string CompanyId { get; set; } = null!;

        public string Name { get; set; } = null!;

        public ICollection<ProjectDto>? Projects { get; set; }
    }
}
