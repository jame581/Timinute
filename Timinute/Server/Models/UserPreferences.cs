using System.ComponentModel.DataAnnotations.Schema;
using Timinute.Shared.Dtos;

namespace Timinute.Server.Models
{
    public class UserPreferences
    {
        public ThemePreference Theme { get; set; } = ThemePreference.System;

        [Column(TypeName = "decimal(4,1)")]
        public decimal WeeklyGoalHours { get; set; } = 32.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal WorkdayHoursPerDay { get; set; } = 8.0m;

        /// <summary>Warn when a running timer reaches this many hours (and each multiple). 0 = off.</summary>
        [Column(TypeName = "decimal(4,1)")]
        public decimal LongTimerWarningHours { get; set; } = 8.0m;

        /// <summary>Offer to trim a running timer after this many idle minutes. 0 = off.</summary>
        public int IdleWarningMinutes { get; set; } = 15;
    }
}
