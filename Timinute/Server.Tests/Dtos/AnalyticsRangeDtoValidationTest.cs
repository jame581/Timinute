using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Timinute.Shared.Dtos.Analytics;
using Xunit;

namespace Timinute.Server.Tests.Dtos
{
    public class AnalyticsRangeDtoValidationTest
    {
        private static List<ValidationResult> Validate(AnalyticsRangeDto dto)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public void Valid_Range_Passes()
        {
            var dto = new AnalyticsRangeDto
            {
                From = DateTimeOffset.UtcNow.AddDays(-7),
                To = DateTimeOffset.UtcNow,
                TzOffsetMinutes = 120
            };
            Assert.Empty(Validate(dto));
        }

        [Fact]
        public void Default_From_Fails()
        {
            var dto = new AnalyticsRangeDto { To = DateTimeOffset.UtcNow };
            Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(AnalyticsRangeDto.From)));
        }

        [Fact]
        public void From_After_To_Fails()
        {
            var dto = new AnalyticsRangeDto
            {
                From = DateTimeOffset.UtcNow,
                To = DateTimeOffset.UtcNow.AddDays(-1)
            };
            Assert.Contains(Validate(dto), r => r.ErrorMessage!.Contains("must be earlier"));
        }

        [Fact]
        public void Range_Over_400_Days_Fails()
        {
            var dto = new AnalyticsRangeDto
            {
                From = DateTimeOffset.UtcNow.AddDays(-401),
                To = DateTimeOffset.UtcNow
            };
            Assert.Contains(Validate(dto), r => r.ErrorMessage!.Contains("400"));
        }

        [Fact]
        public void TzOffset_Beyond_14_Hours_Fails()
        {
            var dto = new AnalyticsRangeDto
            {
                From = DateTimeOffset.UtcNow.AddDays(-1),
                To = DateTimeOffset.UtcNow,
                TzOffsetMinutes = 900
            };
            Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(AnalyticsRangeDto.TzOffsetMinutes)));
        }
    }
}
