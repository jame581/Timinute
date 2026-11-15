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

        // Invariant culture: the limits are "1.0"-style strings, and RangeAttribute otherwise
        // parses them with the server's culture, which throws (500) on cs-CZ, de-DE and the like.
        [Required, Range(typeof(decimal), "1.0", "168.0", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal WeeklyGoalHours { get; set; }

        [Required, Range(typeof(decimal), "0.5", "24.0", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal WorkdayHoursPerDay { get; set; }
    }
}
