using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos
{
    public class UpdateUserPreferencesDto
    {
        [Required]
        public ThemePreference Theme { get; set; }

        [Required, Range(typeof(decimal), "1.0", "168.0")]
        public decimal WeeklyGoalHours { get; set; }

        [Required, Range(typeof(decimal), "0.5", "24.0")]
        public decimal WorkdayHoursPerDay { get; set; }
    }
}
