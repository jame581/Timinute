using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos
{
    public class UpdateUserPreferencesDto
    {
        // Nullable so [Required] actually enforces JSON presence — on a
        // non-nullable enum the binder substitutes the CLR default (System)
        // for a missing field and validation passes, breaking the
        // full-replace contract documented in the spec.
        [Required]
        public ThemePreference? Theme { get; set; }

        [Required, Range(typeof(decimal), "1.0", "168.0")]
        public decimal WeeklyGoalHours { get; set; }

        [Required, Range(typeof(decimal), "0.5", "24.0")]
        public decimal WorkdayHoursPerDay { get; set; }
    }
}
