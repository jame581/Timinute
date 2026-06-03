using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos.Tag
{
    public class CreateTagDto
    {
        [Required]
        [StringLength(30, MinimumLength = 1, ErrorMessage = "Tag name must be between 1 and 30 characters.")]
        public string Name { get; set; } = null!;

        [Required]
        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color (e.g. #6366F1).")]
        public string Color { get; set; } = null!;
    }
}
