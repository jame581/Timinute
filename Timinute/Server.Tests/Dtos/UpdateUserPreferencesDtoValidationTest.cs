using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Timinute.Shared.Dtos;
using Xunit;

namespace Timinute.Server.Tests.Dtos
{
    // The decimal [Range] limits are written as "1.0"-style strings. RangeAttribute parses them
    // with the current culture by default, so on a server running in a comma-decimal culture
    // (cs-CZ, de-DE, ...) every PUT /User/me/preferences threw and returned 500.
    public class UpdateUserPreferencesDtoValidationTest
    {
        private static List<ValidationResult> ValidateIn(string culture, UpdateUserPreferencesDto dto)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            try
            {
                var results = new List<ValidationResult>();
                Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
                return results;
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Theory]
        [InlineData("cs-CZ")]
        [InlineData("de-DE")]
        [InlineData("en-US")]
        public void Valid_Preferences_Pass_In_Any_Culture(string culture)
        {
            var dto = new UpdateUserPreferencesDto { Theme = ThemePreference.Dark, WeeklyGoalHours = 32.5m, WorkdayHoursPerDay = 7.5m };

            Assert.Empty(ValidateIn(culture, dto));
        }

        [Theory]
        [InlineData("cs-CZ")]
        [InlineData("en-US")]
        public void Out_Of_Range_Fails_In_Any_Culture(string culture)
        {
            var dto = new UpdateUserPreferencesDto { Theme = ThemePreference.Dark, WeeklyGoalHours = 200m, WorkdayHoursPerDay = 0.25m };

            Assert.Equal(2, ValidateIn(culture, dto).Count);
        }
    }
}
