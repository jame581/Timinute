using System.ComponentModel.DataAnnotations.Schema;

namespace Timinute.Server.Models
{
    public class UserPreferences
    {
        public ThemePreference Theme { get; set; } = ThemePreference.System;

        [Column(TypeName = "decimal(4,1)")]
        public decimal WeeklyGoalHours { get; set; } = 32.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal WorkdayHoursPerDay { get; set; } = 8.0m;
    }
}
