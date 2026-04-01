using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos.Project
{
    public class CreateProjectDto
    {
        [Required]
        [StringLength(100, ErrorMessage = "Project name must be between 2 and 100 characters.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        public string? CompanyId { get; set; }
    }
}
