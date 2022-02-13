using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Models
{
    public class Project
    {
        public string ProjectId { get; set; } = null!;

        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Name can not have less then 3 characters and more then 50.")]
        public string Name { get; set; } = null!;

        public Project()
        {

        }

        public Project(ProjectDto project)
        {
            ProjectId = project.ProjectId;
            Name = project.Name;
        }
    }
}
