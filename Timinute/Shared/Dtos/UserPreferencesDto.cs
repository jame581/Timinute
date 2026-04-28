namespace Timinute.Shared.Dtos
{
    public class UserPreferencesDto
    {
        public ThemePreference Theme { get; set; } = ThemePreference.System;

        public decimal WeeklyGoalHours { get; set; } = 32.0m;

        public decimal WorkdayHoursPerDay { get; set; } = 8.0m;
    }
}
