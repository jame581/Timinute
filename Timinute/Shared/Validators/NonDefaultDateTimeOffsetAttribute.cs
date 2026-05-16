using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Validators
{
    /// <summary>
    /// Rejects <see cref="System.DateTimeOffset"/> values equal to
    /// <c>default(DateTimeOffset)</c> (year 0001). Required on DTOs where
    /// the field is a non-nullable struct — <c>[Required]</c> on a value
    /// type is a no-op because every value type has a default. Without this
    /// attribute, a JSON payload that omits the property binds to the
    /// type default and passes validation, leading to nonsensical data.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class NonDefaultDateTimeOffsetAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is DateTimeOffset dto)
            {
                return dto != default;
            }
            // Let [Required] / other attributes handle null or wrong-type cases.
            return true;
        }

        public override string FormatErrorMessage(string name)
            => $"The {name} field must be a valid date and time.";
    }
}
