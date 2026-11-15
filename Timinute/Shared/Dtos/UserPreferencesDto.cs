namespace Timinute.Shared.Dtos
{
    public class UserPreferencesDto
    {
        public ThemePreference Theme { get; set; } = ThemePreference.System;

        public decimal WeeklyGoalHours { get; set; } = 32.0m;

        public decimal WorkdayHoursPerDay { get; set; } = 8.0m;

        /// <summary>Hours after which a running timer raises a warning (and every multiple). 0 = off.</summary>
        public decimal LongTimerWarningHours { get; set; } = 8.0m;

        /// <summary>Idle minutes (with a timer running) before offering to trim. 0 = off.</summary>
        public int IdleWarningMinutes { get; set; } = 15;
    }
}
