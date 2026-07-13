using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Validators;

namespace Timinute.Shared.Dtos.Analytics
{
    public class AnalyticsRangeDto : IValidatableObject
    {
        [NonDefaultDateTimeOffset]
        public DateTimeOffset From { get; set; }

        [NonDefaultDateTimeOffset]
        public DateTimeOffset To { get; set; }

        // Client's local UTC offset, used to group per-day buckets by the
        // user's calendar day instead of the UTC day. ±14 h covers all zones.
        // A single fixed offset is applied across the whole range, so day
        // bucketing near midnight can shift by an hour across a DST transition
        // that falls inside the range — accepted design tradeoff.
        [Range(-840, 840, ErrorMessage = "TzOffsetMinutes must be between -840 and 840.")]
        public int TzOffsetMinutes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (From != default && To != default && From > To)
            {
                yield return new ValidationResult(
                    "From must be earlier than or equal to To.",
                    new[] { nameof(From), nameof(To) });
            }

            if (From != default && To != default && (To - From).TotalDays > 400)
            {
                yield return new ValidationResult(
                    "Range must not exceed 400 days.",
                    new[] { nameof(From), nameof(To) });
            }
        }
    }
}
